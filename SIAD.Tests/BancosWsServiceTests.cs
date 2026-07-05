using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.BancosWs;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.BancosWs;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// F8 — capa de servicio del WS bancario (BancosWsService): mapeo de la
// consulta al contrato (SinRegistro / SinPendientes / Vencidas / Ok),
// autenticación por credencial y semántica genkey. Corre dentro de la misma
// transacción del fixture (SiadDbContext enlistado con UseTransaction).
[Collection("Postgres")]
public sealed class BancosWsServiceTests : IntegrationTestBase, IDisposable
{
    private const string Clave = "099999902";
    private const string Banco = "097";

    private SiadDbContext? _context;

    public BancosWsServiceTests(PostgresFixture fixture) : base(fixture) { }

    private BancosWsService CrearServicio()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new CompanyFija(CompanyId));
        _context.Database.UseTransaction(Transaction);
        return new BancosWsService(_context, new CompanyFija(CompanyId));
    }

    public void Dispose() => _context?.Dispose();

    private async Task CrearClienteAsync(string clave = Clave)
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.cliente_maestro
                (company_id, maestro_cliente_clave, maestro_cliente_identidad, maestro_cliente_nombre, estado)
            VALUES (@CompanyId, @Clave, '0000000000000', 'CLIENTE SERVICIO F8 ', true)",
            new { CompanyId, Clave = clave }, Transaction));
    }

    private async Task CrearFacturaAsync(string clave, decimal monto, int antiguedadDias, int? venceEnDias, int correlativo)
    {
        // numrecibo es identity GENERATED ALWAYS: lo asigna la BD.
        var facturaId = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.factura
                (company_id, numfactura, clientecodigo, tipofactura, tipofacturacion,
                 fechaemision, fechavence, periodo, saldototal, usuario, estado, estado_id)
            VALUES (@CompanyId, @NumFactura, @Clave, 'F', 'S',
                    current_date - @Antiguedad,
                    CASE WHEN @VenceEn IS NULL THEN NULL ELSE current_date + @VenceEn END,
                    '2026/6', @Monto, 'test-f8', 'A', 1)
            RETURNING id",
            new
            {
                CompanyId,
                NumFactura = $"TEST-F8-SRV-{correlativo:D3}",
                Clave = clave,
                Antiguedad = antiguedadDias,
                VenceEn = venceEnDias,
                Monto = monto,
            }, Transaction));

        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.factura_detalle
                (company_id, factura_id, codigo, tiposervicio, descripcion, montovalor, montovalor_saldo)
            VALUES (@CompanyId, @FacturaId, '', 'AGUA_POTABLE', 'Agua Potable', @Monto, @Monto)",
            new { CompanyId, FacturaId = facturaId, Monto = monto }, Transaction));
    }

    [SkippableFact]
    public async Task Consulta_cliente_inexistente_devuelve_sin_registro()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var consulta = await servicio.ConsultarAsync("000000000");

        Assert.Equal(BancosWsConsultaResultado.SinRegistro, consulta.Resultado);
    }

    [SkippableFact]
    public async Task Consulta_cliente_sin_deuda_devuelve_sin_pendientes()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await CrearClienteAsync();
        var servicio = CrearServicio();

        var consulta = await servicio.ConsultarAsync(Clave);

        Assert.Equal(BancosWsConsultaResultado.SinPendientes, consulta.Resultado);
    }

    [SkippableFact]
    public async Task Consulta_ok_arma_cabecera_y_detalles_del_contrato()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await CrearClienteAsync();
        await CrearFacturaAsync(Clave, 199.27m, antiguedadDias: 10, venceEnDias: 15, correlativo: 1);
        var servicio = CrearServicio();

        var consulta = await servicio.ConsultarAsync(Clave);

        Assert.Equal(BancosWsConsultaResultado.Ok, consulta.Resultado);
        Assert.Equal(Clave, consulta.Clave);
        // El nombre sale trimmed (el WS viejo hacía trim(nombre) en el SQL).
        Assert.Equal("CLIENTE SERVICIO F8", consulta.Nombre);
        Assert.Equal("N", consulta.FechaVence);
        Assert.Equal(199.27m, consulta.Total);
        var detalle = Assert.Single(consulta.Detalles);
        // codigo vacío en factura_detalle → cae al código del servicio.
        Assert.Equal("AGUA_POTABLE", detalle.CodigoConcepto);
        Assert.Equal("Agua Potable", detalle.Concepto);
        Assert.Equal(199.27m, detalle.Valor);
    }

    [SkippableFact]
    public async Task Consulta_con_factura_vigente_vencida_bloquea()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await CrearClienteAsync();
        await CrearFacturaAsync(Clave, 100.00m, antiguedadDias: 40, venceEnDias: -5, correlativo: 2);
        var servicio = CrearServicio();

        var consulta = await servicio.ConsultarAsync(Clave);

        // Regla SIMAFI replicada (contrato §5.2): vencida ⇒ 400 "Las facturas estan vencidas".
        Assert.Equal(BancosWsConsultaResultado.Vencidas, consulta.Resultado);
    }

    [SkippableFact]
    public async Task Consulta_usa_el_vencimiento_de_la_factura_vigente_no_de_la_vieja()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await CrearClienteAsync();
        // Vieja vencida + vigente al día: en SIMAFI la factura del mes arrastra
        // el saldo anterior y manda SU vence ⇒ se puede cobrar.
        await CrearFacturaAsync(Clave, 100.00m, antiguedadDias: 40, venceEnDias: -10, correlativo: 3);
        await CrearFacturaAsync(Clave, 50.00m, antiguedadDias: 5, venceEnDias: 20, correlativo: 4);
        var servicio = CrearServicio();

        var consulta = await servicio.ConsultarAsync(Clave);

        Assert.Equal(BancosWsConsultaResultado.Ok, consulta.Resultado);
        Assert.Equal(150.00m, consulta.Total);
        Assert.Equal(2, consulta.Detalles.Count);
        // FIFO: la línea de la factura más vieja primero.
        Assert.Equal(100.00m, consulta.Detalles[0].Valor);
    }

    [SkippableFact]
    public async Task Autenticar_valida_banco_llave_y_resuelve_el_tenant()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.ban_ws_credencial (company_id, banco, nombre, llave, created_by)
            VALUES (@CompanyId, @Banco, 'BANCO SRV F8', 'LLAVESRVF8', 'test-f8')
            ON CONFLICT (company_id, banco) DO UPDATE SET llave = 'LLAVESRVF8', activo = true",
            new { CompanyId, Banco }, Transaction));
        var servicio = CrearServicio();

        var ok = await servicio.AutenticarAsync(Banco, "LLAVESRVF8");
        Assert.NotNull(ok);
        Assert.Equal(CompanyId, ok.CompanyId);

        Assert.Null(await servicio.AutenticarAsync(Banco, "LLAVEMALA"));
        Assert.Null(await servicio.AutenticarAsync(Banco, ""));
        Assert.Null(await servicio.AutenticarAsync(null, "LLAVESRVF8"));

        // Credencial inactiva no autentica.
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.ban_ws_credencial SET activo = false WHERE company_id = @CompanyId AND banco = @Banco",
            new { CompanyId, Banco }, Transaction));
        Assert.Null(await servicio.AutenticarAsync(Banco, "LLAVESRVF8"));
    }

    [SkippableFact]
    public async Task Genkey_regenera_llave_hex40_y_no_aplica_a_banco_inexistente()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.ban_ws_credencial (company_id, banco, nombre, llave, created_by)
            VALUES (@CompanyId, @Banco, 'BANCO SRV F8', 'LLAVEVIEJA', 'test-f8')
            ON CONFLICT (company_id, banco) DO UPDATE SET llave = 'LLAVEVIEJA', activo = true",
            new { CompanyId, Banco }, Transaction));
        var servicio = CrearServicio();

        Assert.True(await servicio.GenerarLlaveAsync(Banco, "2027-12-31"));
        Assert.False(await servicio.GenerarLlaveAsync("ZZZ", null));

        var credencial = await Connection.QueryFirstAsync<(string llave, DateTime? vigencia)>(new CommandDefinition(
            "SELECT llave, vigencia FROM public.ban_ws_credencial WHERE company_id = @CompanyId AND banco = @Banco",
            new { CompanyId, Banco }, Transaction));
        Assert.Equal(40, credencial.llave.Length);
        Assert.Equal(credencial.llave.ToUpperInvariant(), credencial.llave);
        Assert.Matches("^[0-9A-F]{40}$", credencial.llave);
        Assert.Equal(new DateTime(2027, 12, 31), credencial.vigencia);

        // Vigencia informativa (nunca bloquea): la credencial con vigencia
        // vencida sigue autenticando, igual que el WS viejo.
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.ban_ws_credencial SET vigencia = DATE '2015-12-30' WHERE company_id = @CompanyId AND banco = @Banco",
            new { CompanyId, Banco }, Transaction));
        Assert.NotNull(await servicio.AutenticarAsync(Banco, credencial.llave));
    }

    private sealed class CompanyFija : ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
