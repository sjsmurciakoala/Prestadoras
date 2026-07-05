using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.MobileApi;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.MobileApi;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.MobileApi;

// L3 — API móvil de lectores (LectoresMobileService). Paridad FUNCIONAL con el
// WS viejo: el servicio llama los MISMOS SPs V3, así que estos tests verifican
// que su salida coincide con la de invocar esos SPs/queries directamente sobre
// la misma siad_v3_test (no se puede correr el WCF .NET Framework en-proceso;
// la referencia es el SP, que es exactamente lo que el WS también ejecuta).
// Todo corre dentro de la transacción del fixture (rollback al final).
[Collection("Postgres")]
public sealed class LectoresMobileServiceTests : IntegrationTestBase, IDisposable
{
    // Datos de ruta abierta sembrados en siad_v3_test (F1–F8 + demo APC).
    private const string Ruta = "01001";
    private const int Ciclo = 1;
    private const int Anio = 2026;
    private const int Mes = 7;

    private SiadDbContext? _context;

    public LectoresMobileServiceTests(PostgresFixture fixture) : base(fixture) { }

    public void Dispose() => _context?.Dispose();

    private LectoresMobileService CrearServicio()
    {
        var options = new DbContextOptionsBuilder<SiadDbContext>()
            .UseNpgsql(Connection)
            .Options;
        _context = new SiadDbContext(options, new CompanyFija(CompanyId));
        _context.Database.UseTransaction(Transaction);
        return new LectoresMobileService(_context);
    }

    private async Task<long> CrearCredencialAsync(string codigo, string clave, string ruta = Ruta)
    {
        return await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.adm_lector_credencial
                (company_id, codigo, clave_hash, lector_nombre, ruta, codciclo, activo, created_by)
            VALUES (@CompanyId, @Codigo, crypt(@Clave, gen_salt('bf')), 'LECTOR TEST L3', @Ruta, 1, true, 'test-l3')
            RETURNING credencial_id;",
            new { CompanyId, Codigo = codigo, Clave = clave, Ruta = ruta }, Transaction));
    }

    // -------------------------------------------------------------------------
    // Autenticación
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task Login_valida_credencial_emite_token_y_sesion()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await CrearCredencialAsync("L3TESTA", "clave-secreta");
        var servicio = CrearServicio();

        var login = await servicio.LoginAsync(new LoginRequest { Codigo = "l3testa", Clave = "clave-secreta", Dispositivo = "dev-1" }, TimeSpan.FromHours(12));

        Assert.NotNull(login);
        Assert.Equal(64, login!.Token.Length);
        Assert.Equal("L3TESTA", login.Lector.Codigo);
        Assert.Equal(Ruta, login.Lector.Ruta);
        Assert.True(login.ExpiraAt > DateTime.UtcNow);

        // El token resuelve la sesión (tenant + ruta) — base del middleware (A6).
        var sesion = await servicio.ValidarSesionAsync(login.Token);
        Assert.NotNull(sesion);
        Assert.Equal(CompanyId, sesion!.CompanyId);
        Assert.Equal("L3TESTA", sesion.Codigo);

        // Logout revoca la sesión.
        await servicio.LogoutAsync(login.Token);
        Assert.Null(await servicio.ValidarSesionAsync(login.Token));
    }

    [SkippableFact]
    public async Task Login_credencial_invalida_o_inactiva_devuelve_null()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        await CrearCredencialAsync("L3TESTB", "clave-buena");
        var servicio = CrearServicio();

        Assert.Null(await servicio.LoginAsync(new LoginRequest { Codigo = "L3TESTB", Clave = "clave-mala" }, TimeSpan.FromHours(12)));
        Assert.Null(await servicio.LoginAsync(new LoginRequest { Codigo = "NO_EXISTE", Clave = "x" }, TimeSpan.FromHours(12)));
        Assert.Null(await servicio.LoginAsync(new LoginRequest { Codigo = "", Clave = "" }, TimeSpan.FromHours(12)));

        // Credencial inactiva no autentica.
        await Connection.ExecuteAsync(new CommandDefinition(
            "UPDATE public.adm_lector_credencial SET activo = false WHERE company_id = @CompanyId AND codigo = 'L3TESTB'",
            new { CompanyId }, Transaction));
        Assert.Null(await servicio.LoginAsync(new LoginRequest { Codigo = "L3TESTB", Clave = "clave-buena" }, TimeSpan.FromHours(12)));
    }

    [SkippableFact]
    public async Task ValidarSesion_token_expirado_devuelve_null()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var credencialId = await CrearCredencialAsync("L3TESTC", "clave");
        await Connection.ExecuteAsync(new CommandDefinition(@"
            INSERT INTO public.adm_lector_sesion (company_id, credencial_id, token, expira_at)
            VALUES (@CompanyId, @CredencialId, 'TOKEN-EXPIRADO-L3', now() - interval '1 hour')",
            new { CompanyId, CredencialId = credencialId }, Transaction));
        var servicio = CrearServicio();

        Assert.Null(await servicio.ValidarSesionAsync("TOKEN-EXPIRADO-L3"));
        Assert.Null(await servicio.ValidarSesionAsync("TOKEN-INEXISTENTE"));
    }

    // -------------------------------------------------------------------------
    // Ciclo / ruta / snapshot: equivalencia con los SPs (paridad V3)
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task GetCiclo_equivale_al_query_V3_directo()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var ciclo = await servicio.GetCicloAsync(Ruta);

        Skip.IfNot(ciclo.Encontrado, "La ruta de prueba no tiene ciclo abierto en esta BD.");
        // Debe coincidir con historialmes (la misma fuente que el CTE del WS).
        var esperado = await Connection.QueryFirstOrDefaultAsync<(int ano, int mes, string ciclo)>(new CommandDefinition(@"
            select hm.ano, hm.mes, btrim(hm.ciclo)::int as ciclo
            from historialmes hm
            where hm.cerrarperiodo = 'P' and hm.cerrado = 'A'
            order by hm.ano desc, hm.mes desc, coalesce(hm.fechaperiodo, hm.fecha::date, now()::date) desc
            limit 1;", transaction: Transaction));
        Assert.Equal(esperado.ano, ciclo.Anio);
        Assert.Equal(esperado.mes, ciclo.Mes);
    }

    [SkippableFact]
    public async Task GetRuta_equivale_al_sp_medidores_por_ruta_ws()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var medidores = await servicio.GetRutaAsync(Ruta, Ciclo, Anio, Mes);

        var esperadoCount = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(
            "select count(*) from public.sp_medidores_por_ruta_ws(@Ruta, @Ciclo, @Anio, @Mes);",
            new { Ruta, Ciclo, Anio, Mes }, Transaction));
        Assert.Equal(esperadoCount, medidores.Count);

        Skip.If(medidores.Count == 0, "La ruta de prueba no tiene medidores en esta BD.");
        var claves = await Connection.QueryAsync<string>(new CommandDefinition(
            "select maestro_cliente_clave from public.sp_medidores_por_ruta_ws(@Ruta, @Ciclo, @Anio, @Mes);",
            new { Ruta, Ciclo, Anio, Mes }, Transaction));
        Assert.Equal(claves.OrderBy(c => c), medidores.Select(m => m.Clave).OrderBy(c => c));
    }

    [SkippableFact]
    public async Task Snapshot_genera_items_con_cai_offline_inyectado()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var snapshot = await servicio.GetSnapshotAsync(Ruta, Ciclo, Anio, Mes);
        Skip.If(snapshot.Items.Count == 0, "La ruta de prueba no tiene medidores en esta BD.");

        Assert.True(snapshot.Success);
        Assert.Equal(OfflineSnapshotRutaDto.SnapshotContractVersion, snapshot.ContractVersion);

        var item = snapshot.Items.First(i => i.Success);
        using var doc = System.Text.Json.JsonDocument.Parse(item.PackageJson);
        // El snapshot del SP + el bloque CAI inyectado por el servicio (como el WS).
        Assert.True(doc.RootElement.TryGetProperty("cai_offline", out var cai));
        Assert.True(cai.TryGetProperty("codigo_cai", out _));
        Assert.Equal(Ruta, cai.GetProperty("ruta_codigo").GetString());
        Assert.True(doc.RootElement.TryGetProperty("cliente_clave", out _));
    }

    // -------------------------------------------------------------------------
    // Subida de lectura: validaciones + idempotencia por UUID
    // -------------------------------------------------------------------------

    [SkippableFact]
    public async Task Lectura_sin_clave_o_sin_cai_falla_con_codigo_de_negocio()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();

        var sinClave = await servicio.ActualizarLecturaAsync(new LecturaV3Request { Clave = "" }, CompanyId);
        Assert.Equal("CLAVE_REQUERIDA", sinClave.Codigo);

        var clave = await PrimeraClaveRutaAsync();
        Skip.If(clave is null, "La ruta de prueba no tiene medidores en esta BD.");

        // CAI parcial (IdCai sin NumeroFactura/Correlativo): CAI_FORMAL_REQUERIDO.
        var caiParcial = await servicio.ActualizarLecturaAsync(
            new LecturaV3Request { Clave = clave!, Anio = Anio, Mes = Mes, LecturaActual = 100, IdCai = 5 }, CompanyId);
        Assert.Equal("CAI_FORMAL_REQUERIDO", caiParcial.Codigo);

        // Sin CAI alguno: sp_lectura_v3 exige número de factura → error de negocio
        // controlado (no excepción cruda).
        var sinCai = await servicio.ActualizarLecturaAsync(
            new LecturaV3Request { Clave = clave!, Anio = Anio, Mes = Mes, LecturaActual = 100 }, CompanyId);
        Assert.Equal("ERROR_LECTURA_V3", sinCai.Codigo);
    }

    [SkippableFact]
    public async Task Lectura_de_tenant_ajeno_es_rechazada()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();
        var clave = await PrimeraClaveRutaAsync();
        Skip.If(clave is null, "La ruta de prueba no tiene medidores en esta BD.");

        // companyId de sesión distinto al del cliente (A6): TENANT_MISMATCH.
        var ajeno = await servicio.ActualizarLecturaAsync(
            new LecturaV3Request
            {
                Clave = clave!, Anio = Anio, Mes = Mes, LecturaActual = 100,
                IdCai = 1, CorrelativoCai = 1, NumeroFactura = "X-1",
            },
            companyId: 999999);
        Assert.Equal("TENANT_MISMATCH", ajeno.Codigo);
    }

    [SkippableFact]
    public async Task Lectura_es_idempotente_por_uuid()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();
        var clave = await PrimeraClaveRutaAsync();
        Skip.If(clave is null, "La ruta de prueba no tiene medidores en esta BD.");

        // Reserva un bloque CAI y elige el correlativo libre más alto del bloque
        // (evita colisión con facturas ya emitidas en la BD de prueba).
        var cai = await Connection.QueryFirstOrDefaultAsync<CaiTest>(new CommandDefinition(@"
            with b as (
                select * from public.sp_adm_obtener_o_reservar_bloque_cai_ruta(@CompanyId, @Ruta, 250, 'test-l3')
            )
            select b.cai_id as IdCai, b.prefijo_documento as Prefijo,
                   (select gs from generate_series(b.correlativo_desde, b.correlativo_hasta) gs
                    where not exists (
                        select 1 from public.factura f
                        where f.clientecodigo = @Clave
                          and f.numfactura = b.prefijo_documento || '-' || lpad(gs::text, 8, '0'))
                    order by gs desc limit 1) as Correlativo
            from b;",
            new { CompanyId, Ruta, Clave = clave }, Transaction));

        Skip.If(cai is null || cai.Correlativo is null || cai.IdCai == 0, "No hay bloque CAI reservable en esta BD.");
        var numeroFactura = $"{cai!.Prefijo}-{cai.Correlativo!.Value:00000000}";

        // Total esperado del servidor (mismo SP que usa el preflight del servicio):
        // se envía en el payload para que el preflight NO marque conflicto.
        var total = await Connection.ExecuteScalarAsync<decimal>(new CommandDefinition(@"
            select total_factura from public.sp_adm_calcular_factura_lectura(
                p_company_id => @CompanyId, p_anio => @Anio, p_mes => @Mes,
                p_cliente_id => (select maestro_cliente_id from public.cliente_maestro
                                 where maestro_cliente_clave = @Clave and estado = true limit 1),
                p_fecha_lectura => current_date, p_lectura_actual => 150::numeric,
                p_condicion_lectura => 'N', p_usuario => 'test-l3',
                p_id_cai => @IdCai, p_correlativo_cai => @Correlativo, p_numero_factura => @NumeroFactura);",
            new { CompanyId, Anio, Mes, Clave = clave, IdCai = (int)cai.IdCai, Correlativo = (int)cai.Correlativo!.Value, NumeroFactura = numeroFactura },
            Transaction));

        LecturaV3Request Pedido() => new()
        {
            Clave = clave!, Anio = Anio, Mes = Mes, LecturaActual = 150, CondicionLectura = "N",
            Usuario = "test-l3", IdCai = (int)cai.IdCai, CorrelativoCai = (int)cai.Correlativo!.Value,
            NumeroFactura = numeroFactura, LecturaUuid = "UUID-L3-IDEMP-TEST", Total = total,
        };

        var primera = await servicio.ActualizarLecturaAsync(Pedido(), CompanyId);
        Skip.IfNot(primera.Success, $"La primera subida no emitió factura ({primera.Codigo}: {primera.Mensaje}).");
        Assert.True(primera.FacturaId > 0);

        // Segunda subida con el MISMO UUID/numero: idempotente, misma factura,
        // sin volver a llamar sp_lectura_v3 (que rechazaría el duplicado).
        var segunda = await servicio.ActualizarLecturaAsync(Pedido(), CompanyId);
        Assert.Equal("IDEMPOTENTE", segunda.Codigo);
        Assert.True(segunda.Success);
        Assert.Equal(primera.FacturaId, segunda.FacturaId);
    }

    [SkippableFact]
    public async Task Lectura_con_total_distinto_al_servidor_devuelve_conflicto()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();
        var clave = await PrimeraClaveRutaAsync();
        Skip.If(clave is null, "La ruta de prueba no tiene medidores en esta BD.");

        var cai = await Connection.QueryFirstOrDefaultAsync<CaiTest>(new CommandDefinition(@"
            with b as (
                select * from public.sp_adm_obtener_o_reservar_bloque_cai_ruta(@CompanyId, @Ruta, 250, 'test-l3')
            )
            select b.cai_id as IdCai, b.prefijo_documento as Prefijo, b.correlativo_hasta as Correlativo from b;",
            new { CompanyId, Ruta }, Transaction));
        Skip.If(cai is null || cai.Correlativo is null || cai.IdCai == 0, "No hay bloque CAI reservable en esta BD.");

        // Total del dispositivo deliberadamente distinto al del servidor.
        var conflicto = await servicio.ActualizarLecturaAsync(new LecturaV3Request
        {
            Clave = clave!, Anio = Anio, Mes = Mes, LecturaActual = 150, CondicionLectura = "N",
            Usuario = "test-l3", IdCai = (int)cai!.IdCai, CorrelativoCai = (int)cai.Correlativo!.Value,
            NumeroFactura = $"{cai.Prefijo}-{cai.Correlativo.Value:00000000}",
            LecturaUuid = "UUID-L3-CONFLICTO", Total = 0.01m,
        }, CompanyId);

        Assert.Equal("SYNC_CONFLICT_TOTAL", conflicto.Codigo);
        Assert.False(conflicto.Success);
    }

    [SkippableFact]
    public async Task Lectura_sin_total_no_dispara_preflight()
    {
        Skip.IfNot(Fixture.Available, "BD de pruebas no disponible.");
        var servicio = CrearServicio();
        var clave = await PrimeraClaveRutaAsync();
        Skip.If(clave is null, "La ruta de prueba no tiene medidores en esta BD.");

        var cai = await Connection.QueryFirstOrDefaultAsync<CaiTest>(new CommandDefinition(@"
            with b as (
                select * from public.sp_adm_obtener_o_reservar_bloque_cai_ruta(@CompanyId, @Ruta, 250, 'test-l3')
            )
            select b.cai_id as IdCai, b.prefijo_documento as Prefijo, b.correlativo_hasta as Correlativo from b;",
            new { CompanyId, Ruta }, Transaction));
        Skip.If(cai is null || cai.Correlativo is null || cai.IdCai == 0, "No hay bloque CAI reservable en esta BD.");

        // Sin Total (nulo): el preflight NO corre → no hay SYNC_CONFLICT_TOTAL.
        var sinTotal = await servicio.ActualizarLecturaAsync(new LecturaV3Request
        {
            Clave = clave!, Anio = Anio, Mes = Mes, LecturaActual = 150, CondicionLectura = "N",
            Usuario = "test-l3", IdCai = (int)cai!.IdCai, CorrelativoCai = (int)cai.Correlativo!.Value,
            NumeroFactura = $"{cai.Prefijo}-{cai.Correlativo.Value:00000000}",
            LecturaUuid = "UUID-L3-SIN-TOTAL", Total = null,
        }, CompanyId);

        Assert.NotEqual("SYNC_CONFLICT_TOTAL", sinTotal.Codigo);
        Assert.True(sinTotal.Success, $"Esperaba éxito, fue {sinTotal.Codigo}: {sinTotal.Mensaje}");
    }

    private async Task<string?> PrimeraClaveRutaAsync()
    {
        var clave = await Connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            "select maestro_cliente_clave from public.sp_medidores_por_ruta_ws(@Ruta, @Ciclo, @Anio, @Mes) limit 1;",
            new { Ruta, Ciclo, Anio, Mes }, Transaction));
        return clave;
    }

    private sealed class CaiTest
    {
        public long IdCai { get; init; }
        public string Prefijo { get; init; } = string.Empty;
        public long? Correlativo { get; init; }
    }

    private sealed class CompanyFija : ICurrentCompanyService
    {
        private readonly long _companyId;
        public CompanyFija(long companyId) => _companyId = companyId;
        public long GetCompanyId() => _companyId;
    }
}
