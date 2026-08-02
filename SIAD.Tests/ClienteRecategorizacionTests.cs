using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Clientes;
using SIAD.Tests.Infrastructure;
using Xunit;

namespace SIAD.Tests;

// Backlog pruebas operativas jul-2026 (suelto final): al cambiar la categoría
// de un cliente (p.ej. Doméstico → Comercial) con CxC POR_SERVICIO_CATEGORIA,
// el saldo pendiente se reclasifica contablemente (DEBE CxC nueva / HABER CxC
// vieja) y las facturas vivas actualizan su snapshot de categoría para que
// los cobros futuros acrediten la cuenta nueva.
[Collection("Postgres")]
public sealed class ClienteRecategorizacionTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private IClientesService? _service;

    public ClienteRecategorizacionTests(PostgresFixture fixture) : base(fixture) { }

    public new async Task InitializeAsync()
    {
        await base.InitializeAsync();

        if (Fixture.Available)
        {
            var options = new DbContextOptionsBuilder<SiadDbContext>()
                .UseNpgsql(Connection)
                .Options;

            var companyService = new TestCurrentCompanyService(CompanyId);
            _context = new SiadDbContext(options, companyService);
            _context.Database.UseTransaction(Transaction);
            _service = new ClientesService(_context, companyService);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Cambio_de_categoria_reclasifica_cxc_y_actualiza_snapshot()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // ---- Arrange contable: perfil ERSAPS + asiento VENTAS + período abierto ----
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-recat')",
            new { CompanyId }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.con_integracion_config
            SET modo_cxc = 'POR_SERVICIO_CATEGORIA', activo_facturacion = true, encolar_sin_periodo = true
            WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));

        var asientoOk = await Connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            INSERT INTO public.con_integracion_asiento (company_id, module, journal_id, type_id, created_by)
            SELECT @CompanyId, 'VENTAS',
                   (SELECT journal_id FROM public.con_diario WHERE company_id = @CompanyId AND is_active ORDER BY journal_id LIMIT 1),
                   (SELECT type_id FROM public.con_tipo_transaccion WHERE company_id = @CompanyId ORDER BY type_id LIMIT 1),
                   'test-recat'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId }, Transaction));
        Skip.IfNot(asientoOk, "Falta diario/tipo en la BD de pruebas.");

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.con_periodo_contable
                (company_id, code, name, start_date, end_date, status_id, status, created_at, created_by)
            SELECT @CompanyId, 'RECAT-TEST', 'Periodo test recategorizacion',
                   current_date - 1, current_date + 1, 0, 'OPEN', now(), 'test-recat'
            WHERE NOT EXISTS (
                SELECT 1 FROM public.con_periodo_contable p
                WHERE p.company_id = @CompanyId AND COALESCE(p.status_id, 2) = 0
                  AND current_date BETWEEN p.start_date::date AND p.end_date::date)",
            new { CompanyId }, Transaction));

        // ---- Dos categorías con cuentas CxC DISTINTAS para el mismo servicio ----
        var dims = await Connection.QueryFirstOrDefaultAsync<(string ServicioCodigo, int Cat1, int Cat2)>(new CommandDefinition(@"
            SELECT s.codigo, a.categoria_servicio_id, b.categoria_servicio_id
            FROM public.con_integracion_cuenta a
            JOIN public.con_integracion_cuenta b
              ON b.company_id = a.company_id AND b.uso = a.uso
             AND b.servicio_id = a.servicio_id
             AND COALESCE(b.con_medicion, false) = COALESCE(a.con_medicion, false)
             AND b.categoria_servicio_id > a.categoria_servicio_id
             AND b.account_id <> a.account_id
            JOIN public.adm_servicio s ON s.servicio_id = a.servicio_id AND s.company_id = a.company_id
            WHERE a.company_id = @CompanyId AND a.uso = 'CXC'
              AND a.servicio_id IS NOT NULL AND a.categoria_servicio_id IS NOT NULL
              AND COALESCE(a.con_medicion, false) = true
            ORDER BY a.servicio_id, a.categoria_servicio_id
            LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(string.IsNullOrWhiteSpace(dims.ServicioCodigo),
            "La matriz ERSAPS no tiene dos categorías con cuentas CxC distintas para un mismo servicio.");

        // ---- Cliente con factura viva de 250.00, snapshot en la categoría vieja ----
        var clave = $"RC{Guid.NewGuid():N}"[..12];
        var clienteId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre,
                 categoria_servicio_id, maestro_cliente_tiene_medidor, estado)
            VALUES (@CompanyId, @Clave, '', 'CLIENTE RECATEGORIZACION', @Cat1, true, true)
            RETURNING maestro_cliente_id",
            new { CompanyId, Clave = clave, Cat1 = dims.Cat1 }, Transaction));

        var facturaId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.factura
                (company_id, numfactura, clientecodigo, tipofactura, tipofacturacion,
                 fechaemision, periodo, saldototal, usuario, estado, estado_id,
                 categoria_servicio_id, con_medicion)
            VALUES (@CompanyId, @NumFactura, @Clave, 'F', 'S',
                    current_date, '2026/8', 250.00, 'test-recat', 'A', 1, @Cat1, true)
            RETURNING id",
            new { CompanyId, NumFactura = $"REC-{clave}", Clave = clave, Cat1 = dims.Cat1 }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle
                (company_id, factura_id, codigo, tiposervicio, descripcion, montovalor, montovalor_saldo)
            VALUES (@CompanyId, @FacturaId, '', @Servicio, 'Servicio test recategorizacion', 250.00, 250.00)",
            new { CompanyId, FacturaId = facturaId, Servicio = dims.ServicioCodigo }, Transaction));

        // ---- Act: cambiar la categoría vía el servicio (flujo real de edición) ----
        await _service!.ActualizarClienteAsync(clienteId, new ClienteUpdateDto
        {
            Clave = clave,
            Nombre = "CLIENTE RECATEGORIZACION",
            CategoriaServicioId = dims.Cat2,
            TieneMedidor = true,
            Activo = true
        }, "test-recat");

        // ---- Assert: bitácora, partida balanceada en las cuentas correctas, snapshot ----
        var evento = await Connection.QueryFirstAsync<(long Id, long? PolizaId, decimal Monto, int Facturas)>(new CommandDefinition(@"
            SELECT id, poliza_id, monto_reclasificado, facturas_actualizadas
            FROM public.cln_cliente_recategorizacion
            WHERE company_id = @CompanyId AND maestro_cliente_id = @ClienteId
            ORDER BY id DESC LIMIT 1",
            new { CompanyId, ClienteId = clienteId }, Transaction));

        Assert.Equal(250.00m, evento.Monto);
        Assert.Equal(1, evento.Facturas);
        Assert.NotNull(evento.PolizaId);

        long ResolverCuenta(int categoriaId) => Connection.ExecuteScalar<long>(@"
            SELECT public.fn_con_resolver_cuenta_modo(
                @CompanyId, 'CXC', 'POR_SERVICIO_CATEGORIA',
                (SELECT servicio_id FROM public.adm_servicio
                 WHERE company_id = @CompanyId AND upper(btrim(codigo)) = upper(btrim(@Servicio))
                 ORDER BY servicio_id LIMIT 1),
                @CategoriaId, true)",
            new { CompanyId, Servicio = dims.ServicioCodigo, CategoriaId = categoriaId }, Transaction);

        var cuentaVieja = ResolverCuenta(dims.Cat1);
        var cuentaNueva = ResolverCuenta(dims.Cat2);
        Assert.NotEqual(cuentaVieja, cuentaNueva);

        var lineas = (await Connection.QueryAsync<(long Cuenta, decimal Debe, decimal Haber)>(new CommandDefinition(@"
            SELECT d.account_id, COALESCE(d.debit_amount, 0), COALESCE(d.credit_amount, 0)
            FROM public.con_partida_dtl d
            WHERE d.company_id = @CompanyId AND d.poliza_id = @PolizaId",
            new { CompanyId, PolizaId = evento.PolizaId }, Transaction))).ToList();

        Assert.Equal(lineas.Sum(l => l.Debe), lineas.Sum(l => l.Haber));
        Assert.Equal(250.00m, lineas.Where(l => l.Cuenta == cuentaNueva).Sum(l => l.Debe));
        Assert.Equal(250.00m, lineas.Where(l => l.Cuenta == cuentaVieja).Sum(l => l.Haber));

        var snapshot = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT categoria_servicio_id FROM public.factura WHERE id = @Id",
            new { Id = facturaId }, Transaction));
        Assert.Equal(dims.Cat2, snapshot);
    }

    /// <summary>
    /// Editar sin tocar la categoría NO genera bitácora ni partida — la
    /// reclasificación solo dispara con un cambio real de categoría.
    /// </summary>
    [SkippableFact]
    public async Task Editar_sin_cambiar_categoria_no_reclasifica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var clave = $"RN{Guid.NewGuid():N}"[..12];
        var categoria = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT categoria_servicio_id FROM public.categoria_servicio ORDER BY categoria_servicio_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(categoria is null, "No hay categorías de servicio en la BD de pruebas.");

        var clienteId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre,
                 categoria_servicio_id, estado)
            VALUES (@CompanyId, @Clave, '', 'CLIENTE SIN CAMBIO', @Categoria, true)
            RETURNING maestro_cliente_id",
            new { CompanyId, Clave = clave, Categoria = categoria }, Transaction));

        await _service!.ActualizarClienteAsync(clienteId, new ClienteUpdateDto
        {
            Clave = clave,
            Nombre = "CLIENTE SIN CAMBIO",
            Apellidos = "EDITADO",
            CategoriaServicioId = categoria,
            Activo = true
        }, "test-recat");

        var eventos = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(
            "SELECT count(*) FROM public.cln_cliente_recategorizacion WHERE company_id = @CompanyId AND maestro_cliente_id = @ClienteId",
            new { CompanyId, ClienteId = clienteId }, Transaction));
        Assert.Equal(0, eventos);
    }

    private sealed class TestCurrentCompanyService : ICurrentCompanyService
    {
        private readonly long _companyId;
        public TestCurrentCompanyService(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
