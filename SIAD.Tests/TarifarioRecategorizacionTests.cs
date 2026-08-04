using System;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Tarifario;
using SIAD.Tests.Infrastructure;
using Xunit;

namespace SIAD.Tests;

// Backlog pruebas operativas: el cambio de categoría REAL de operación ocurre
// en /tarifario/cliente-servicio-v3 (categoría REGULATORIA de la asignación de
// servicio), no en Editar Cliente. Ese guardado debe sincronizar el equivalente
// contable (cliente_maestro.categoria_servicio_id) y reclasificar el saldo CxC
// pendiente — misma bitácora y partida que el flujo de Editar Cliente.
[Collection("Postgres")]
public sealed class TarifarioRecategorizacionTests : IntegrationTestBase, IAsyncLifetime
{
    private SiadDbContext? _context;
    private ClienteServicioTarifarioService? _service;

    public TarifarioRecategorizacionTests(PostgresFixture fixture) : base(fixture) { }

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
            _service = new ClienteServicioTarifarioService(_context, companyService);
        }
    }

    public new Task DisposeAsync()
    {
        _context?.Dispose();
        return base.DisposeAsync();
    }

    [SkippableFact]
    public async Task Guardar_servicio_con_otra_categoria_sincroniza_maestro_y_reclasifica()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        // ---- Arrange contable (mismo andamiaje que ClienteRecategorizacionTests) ----
        await Connection.ExecuteAsync(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test-tarif-recat')",
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
                   'test-tarif-recat'
            ON CONFLICT (company_id, module)
            DO UPDATE SET journal_id = EXCLUDED.journal_id, type_id = EXCLUDED.type_id
            RETURNING journal_id IS NOT NULL AND type_id IS NOT NULL",
            new { CompanyId }, Transaction));
        Skip.IfNot(asientoOk, "Falta diario/tipo en la BD de pruebas.");

        // ---- Dos categorías contables con cuentas CxC distintas ----
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
            "La matriz ERSAPS no tiene dos categorías con cuentas CxC distintas.");

        // ---- Categoría regulatoria cuyo equivalente contable es Cat2 ----
        var catRegulatoria = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            UPDATE public.adm_categoria_regulatoria
            SET categoria_servicio_id = @Cat2
            WHERE company_id = @CompanyId
              AND categoria_regulatoria_id = (
                  SELECT categoria_regulatoria_id FROM public.adm_categoria_regulatoria
                  WHERE company_id = @CompanyId ORDER BY categoria_regulatoria_id LIMIT 1)
            RETURNING categoria_regulatoria_id",
            new { CompanyId, Cat2 = dims.Cat2 }, Transaction));
        Skip.If(catRegulatoria is null, "No hay categorías regulatorias en la BD de pruebas.");

        var servicioV3 = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT servicio_id FROM public.adm_servicio WHERE company_id = @CompanyId ORDER BY servicio_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(servicioV3 is null, "No hay servicios V3 en la BD de pruebas.");

        // ---- Cliente en Cat1 con factura viva de 180.00 ----
        var clave = $"TR{Guid.NewGuid():N}"[..12];
        var clienteId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre,
                 categoria_servicio_id, maestro_cliente_tiene_medidor, estado)
            VALUES (@CompanyId, @Clave, '', 'CLIENTE TARIFARIO RECAT', @Cat1, true, true)
            RETURNING maestro_cliente_id",
            new { CompanyId, Clave = clave, Cat1 = dims.Cat1 }, Transaction));

        var facturaId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.factura
                (company_id, numfactura, clientecodigo, tipofactura, tipofacturacion,
                 fechaemision, periodo, saldototal, usuario, estado, estado_id,
                 categoria_servicio_id, con_medicion)
            VALUES (@CompanyId, @NumFactura, @Clave, 'F', 'S',
                    current_date, '2026/8', 180.00, 'test-tarif-recat', 'A', 1, @Cat1, true)
            RETURNING id",
            new { CompanyId, NumFactura = $"TRC-{clave}", Clave = clave, Cat1 = dims.Cat1 }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle
                (company_id, factura_id, codigo, tiposervicio, descripcion, montovalor, montovalor_saldo)
            VALUES (@CompanyId, @FacturaId, '', @Servicio, 'Servicio test tarifario', 180.00, 180.00)",
            new { CompanyId, FacturaId = facturaId, Servicio = dims.ServicioCodigo }, Transaction));

        // ---- Act: guardar la asignación por el flujo REAL del portal V3 ----
        var respuesta = await _service!.GuardarAsync(clienteId, new ClienteServicioSaveRequest(
            ClienteServicioId: null,
            ServicioId: servicioV3.Value,
            CategoriaRegulatoriaId: catRegulatoria,
            CondicionMedicionId: null,
            SegmentoTarifarioId: null,
            FechaAlta: DateTime.Today), "test-tarif-recat");

        Assert.True(respuesta.Success, respuesta.Message);
        Assert.Contains("reclasific", respuesta.Message, StringComparison.OrdinalIgnoreCase);

        // ---- Assert: maestro sincronizado, bitácora, snapshot ----
        var categoriaMaestro = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT categoria_servicio_id FROM public.cliente_maestro WHERE maestro_cliente_id = @Id",
            new { Id = clienteId }, Transaction));
        Assert.Equal(dims.Cat2, categoriaMaestro);

        var evento = await Connection.QueryFirstAsync<(decimal Monto, int Facturas, int? CatAnterior, int? CatNueva)>(new CommandDefinition(@"
            SELECT monto_reclasificado, facturas_actualizadas, categoria_anterior_id, categoria_nueva_id
            FROM public.cln_cliente_recategorizacion
            WHERE company_id = @CompanyId AND maestro_cliente_id = @ClienteId
            ORDER BY id DESC LIMIT 1",
            new { CompanyId, ClienteId = clienteId }, Transaction));
        Assert.Equal(180.00m, evento.Monto);
        Assert.Equal(1, evento.Facturas);
        Assert.Equal(dims.Cat1, evento.CatAnterior);
        Assert.Equal(dims.Cat2, evento.CatNueva);

        var snapshot = await Connection.ExecuteScalarAsync<int?>(new CommandDefinition(
            "SELECT categoria_servicio_id FROM public.factura WHERE id = @Id",
            new { Id = facturaId }, Transaction));
        Assert.Equal(dims.Cat2, snapshot);
    }

    /// <summary>
    /// Guardar con la MISMA categoría (o una sin equivalencia) no toca el
    /// maestro ni genera bitácora.
    /// </summary>
    [SkippableFact]
    public async Task Guardar_sin_cambio_de_categoria_no_toca_al_cliente()
    {
        Skip.IfNot(Fixture.Available, "SIAD_TEST_DB no configurado");

        var catRegulatoria = await Connection.QueryFirstOrDefaultAsync<(long Id, int? Equivalente)>(new CommandDefinition(@"
            SELECT categoria_regulatoria_id, categoria_servicio_id
            FROM public.adm_categoria_regulatoria
            WHERE company_id = @CompanyId AND categoria_servicio_id IS NOT NULL
            ORDER BY categoria_regulatoria_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(catRegulatoria.Id == 0, "No hay categorías regulatorias con equivalencia en la BD de pruebas.");

        var servicioV3 = await Connection.ExecuteScalarAsync<long?>(new CommandDefinition(
            "SELECT servicio_id FROM public.adm_servicio WHERE company_id = @CompanyId ORDER BY servicio_id LIMIT 1",
            new { CompanyId }, Transaction));
        Skip.If(servicioV3 is null, "No hay servicios V3 en la BD de pruebas.");

        var clave = $"TS{Guid.NewGuid():N}"[..12];
        var clienteId = await Connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre,
                 categoria_servicio_id, estado)
            VALUES (@CompanyId, @Clave, '', 'CLIENTE SIN CAMBIO TARIF', @Categoria, true)
            RETURNING maestro_cliente_id",
            new { CompanyId, Clave = clave, Categoria = catRegulatoria.Equivalente }, Transaction));

        var respuesta = await _service!.GuardarAsync(clienteId, new ClienteServicioSaveRequest(
            ClienteServicioId: null,
            ServicioId: servicioV3.Value,
            CategoriaRegulatoriaId: catRegulatoria.Id,
            CondicionMedicionId: null,
            SegmentoTarifarioId: null,
            FechaAlta: DateTime.Today), "test-tarif-recat");

        Assert.True(respuesta.Success, respuesta.Message);

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
