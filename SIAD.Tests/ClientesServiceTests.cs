using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Xunit;
using SIAD.Core.Tenancy;
using SIAD.Services.Clientes;
using SIAD.Data;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

[Collection("Postgres")]
public class ClientesServiceTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IClientesService? _service;

    public ClientesServiceTests(PostgresFixture fixture) : base(fixture)
    {
    }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var mockCompanyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, mockCompanyService);
            _context.Database.UseTransaction(Transaction);
            
            _service = new ClientesService(_context, mockCompanyService);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Test_GetMovimientosPagedAsync_Succeeds()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // El mirror es un restore de producción: no hay ids fijos garantizados. Se elige el cliente
        // con MÁS movimientos en el histórico (transaccion_abonado, la fuente que usa el servicio),
        // en vez de un id hardcodeado que podría no existir o no tener movimientos en la base actual.
        var clienteId = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
            SELECT cm.maestro_cliente_id
            FROM public.transaccion_abonado t
            JOIN public.cliente_maestro cm
              ON cm.company_id = t.company_id AND cm.maestro_cliente_clave = t.cliente_clave
            WHERE t.company_id = @CompanyId
            GROUP BY cm.maestro_cliente_id
            ORDER BY COUNT(*) DESC
            LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(clienteId is null, "El mirror no tiene clientes con movimientos en el histórico.");

        // Act
        var result = await _service!.GetMovimientosPagedAsync(
            clienteId: clienteId!.Value,
            skip: 0,
            take: 20,
            sortField: null,
            sortDesc: false);

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Items);
        Assert.True(result.Items.Count > 0, "Debe retornar al menos un movimiento.");
    }

    /// <summary>
    /// Pruebas operativas jul-2026: los clientes migrados de SIMAFI no traen
    /// identidad — el DNI obligatorio bloqueaba GUARDAR cualquier edición
    /// ("error técnico al actualizar clientes existentes"). El DNI es opcional.
    /// </summary>
    [SkippableFact]
    public async Task Actualizar_cliente_migrado_sin_dni_guarda()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = $"MIG-{Guid.NewGuid():N}"[..12];
        var clienteId = await Dapper.SqlMapper.ExecuteScalarAsync<int>(Connection, new Dapper.CommandDefinition(@"
            INSERT INTO public.cliente_maestro (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@companyId, @clave, '', 'Cliente Migrado Sin DNI', true)
            RETURNING maestro_cliente_id",
            new { companyId = CompanyId, clave }, Transaction));

        var actualizado = await _service!.ActualizarClienteAsync(clienteId, new SIAD.Core.DTOs.Clientes.ClienteUpdateDto
        {
            Clave = clave,
            Nombre = "Cliente Migrado",
            Apellidos = "Editado",
            Dni = null,          // sin identidad, como llegó de SIMAFI
            Rtn = null,
            Activo = true
        }, "test");

        // Lo importante: GUARDÓ sin exigir DNI (antes reventaba aquí).
        Assert.NotNull(actualizado);
        Assert.Contains("Editado", $"{actualizado.Nombre} {actualizado.Apellidos}");

        // Y si el DNI viene informado, la unicidad sigue vigente.
        var otroId = await Dapper.SqlMapper.ExecuteScalarAsync<int>(Connection, new Dapper.CommandDefinition(@"
            INSERT INTO public.cliente_maestro (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@companyId, @clave, '0801199912345', 'Cliente Con DNI', true)
            RETURNING maestro_cliente_id",
            new { companyId = CompanyId, clave = clave + "B" }, Transaction));

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _service!.ActualizarClienteAsync(clienteId, new SIAD.Core.DTOs.Clientes.ClienteUpdateDto
            {
                Clave = clave,
                Nombre = "Cliente Migrado",
                Dni = "0801199912345",   // duplicado del otro
                Activo = true
            }, "test"));
    }

    /// <summary>
    /// Pruebas operativas jul-2026: filtro por rango de fechas en movimientos.
    /// Las facturas del escenario NO tienen espejo congelado, así que además
    /// fija que los documentos post-corte SÍ aparecen en el estado de cuenta
    /// (el espejo murió en F7 H4) y que el saldo corrido es el histórico real
    /// aunque la ventana recorte filas.
    /// </summary>
    [SkippableFact]
    public async Task Movimientos_filtra_por_fechas_e_incluye_documentos_post_corte()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = $"MOV-{Guid.NewGuid():N}"[..12];
        var clienteId = await Dapper.SqlMapper.ExecuteScalarAsync<int>(Connection, new Dapper.CommandDefinition(@"
            INSERT INTO public.cliente_maestro (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@companyId, @clave, '', 'Cliente Movimientos', true)
            RETURNING maestro_cliente_id",
            new { companyId = CompanyId, clave }, Transaction));

        // Dos facturas post-corte (sin espejo) en meses distintos, 100 y 50.
        foreach (var (fecha, monto, suf) in new[] { ("2026-06-10", 100m, "JUN"), ("2026-07-10", 50m, "JUL") })
        {
            var facturaId = await Dapper.SqlMapper.ExecuteScalarAsync<int>(Connection, new Dapper.CommandDefinition(@"
                INSERT INTO public.factura (company_id, numfactura, clientecodigo, tipofactura,
                    ano, mes, fechaemision, estado, tipofacturacion, tipo_documento_fiscal_id)
                VALUES (@companyId, @num, @clave, 'F', '2026', '7', @fecha::date, 'A', 'S', 1)
                RETURNING id",
                new { companyId = CompanyId, num = $"MOVF-{clave}-{suf}", clave, fecha }, Transaction));

            await Dapper.SqlMapper.ExecuteAsync(Connection, new Dapper.CommandDefinition(@"
                INSERT INTO public.factura_detalle (company_id, factura_id, codigo, tiposervicio, montovalor, montovalor_saldo)
                VALUES (@companyId, @facturaId, 'AGUA_POTABLE', 'AGUA_POTABLE', @monto, @monto)",
                new { companyId = CompanyId, facturaId, monto }, Transaction));
        }

        // Sin filtro: los dos documentos, saldo corrido 100 → 150.
        var todo = await _service!.GetMovimientosPagedAsync(clienteId, 0, 20, "Fecha", false);
        Assert.Equal(2, todo.TotalCount);
        Assert.Equal(150m, todo.Items[^1].SaldoInline);

        // Ventana de julio: SOLO la factura de julio, pero con el saldo
        // corrido acumulado real (150), no recalculado desde cero.
        var julio = await _service!.GetMovimientosPagedAsync(
            clienteId, 0, 20, "Fecha", false,
            desde: new DateOnly(2026, 7, 1), hasta: new DateOnly(2026, 7, 31));
        Assert.Equal(1, julio.TotalCount);
        Assert.Contains("JUL", julio.Items[0].NumFactura);
        Assert.Equal(150m, julio.Items[0].SaldoInline);
    }

    private class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
