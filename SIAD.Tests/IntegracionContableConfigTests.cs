using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests;

// Fase 1 del plan de integración contable-comercial (2026-07-02):
// modelo de configuración (con_integracion_config / con_integracion_cuenta),
// resolución con fallback (fn_con_resolver_cuenta) y perfil de auto-llenado
// ERSAPS (sp_con_aplicar_perfil_integracion). Requiere una BD con el plan
// ERSAPS cargado y el script 20260702_ci_fase1_integracion_config.sql aplicado.
[Collection("Postgres")]
public sealed class IntegracionContableConfigTests : IntegrationTestBase
{
    public IntegracionContableConfigTests(PostgresFixture fixture) : base(fixture) { }

    private sealed class PerfilResult
    {
        public int insertadas { get; set; }
        public int existentes { get; set; }
        public int sin_cuenta { get; set; }
    }

    /// <summary>
    /// Deja la configuración de integración vacía y aplica el perfil ERSAPS
    /// dentro de la transacción del test.
    /// </summary>
    private async Task<PerfilResult> AplicarPerfilDesdeCeroAsync()
    {
        await Connection.ExecuteAsync(new CommandDefinition(@"
            DELETE FROM public.con_integracion_cuenta WHERE company_id = @CompanyId;
            DELETE FROM public.con_integracion_config WHERE company_id = @CompanyId;",
            new { CompanyId }, Transaction));

        return await Connection.QueryFirstAsync<PerfilResult>(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test')",
            new { CompanyId }, Transaction));
    }

    private Task<long?> ServicioIdAsync(string codigo) =>
        Connection.ExecuteScalarAsync<long?>(new CommandDefinition(@"
            SELECT servicio_id FROM public.adm_servicio
            WHERE company_id = @CompanyId AND status_id = 1 AND UPPER(BTRIM(codigo)) = @Codigo",
            new { CompanyId, Codigo = codigo }, Transaction));

    private Task<int?> CategoriaIdAsync(string patron) =>
        Connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
            SELECT categoria_servicio_id FROM public.categoria_servicio
            WHERE estado AND lower(descripcion) LIKE @Patron
            ORDER BY categoria_servicio_id LIMIT 1",
            new { Patron = patron }, Transaction));

    private Task<string?> ResolverCodigoAsync(
        string uso, long? servicioId = null, int? categoriaId = null, bool? conMedicion = null) =>
        Connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT c.code FROM public.con_plan_cuentas c
            WHERE c.account_id = public.fn_con_resolver_cuenta(
                @CompanyId, @Uso, @ServicioId, @CategoriaId, @ConMedicion)",
            new { CompanyId, Uso = uso, ServicioId = servicioId, CategoriaId = categoriaId, ConMedicion = conMedicion },
            Transaction));

    // ------------------------------------------------------------------
    // Perfil ERSAPS
    // ------------------------------------------------------------------

    [SkippableFact]
    public async Task Perfil_ERSAPS_llena_matriz_completa()
    {
        var resultado = await AplicarPerfilDesdeCeroAsync();

        Assert.True(resultado.insertadas > 0, "El perfil no insertó filas.");
        Assert.Equal(0, resultado.sin_cuenta);

        // Cabecera con granularidad máxima.
        var modos = await Connection.QueryFirstAsync<(string ventas, string cxc)>(new CommandDefinition(
            "SELECT modo_ventas, modo_cxc FROM public.con_integracion_config WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));
        Assert.Equal("POR_SERVICIO_CATEGORIA", modos.ventas);
        Assert.Equal("POR_SERVICIO_CATEGORIA", modos.cxc);

        // Fila general para cada uso salvo DEVOLUCION_NC (deliberadamente sin
        // configurar: la NC espeja la factura origen).
        var usosGenerales = (await Connection.QueryAsync<string>(new CommandDefinition(@"
            SELECT uso FROM public.con_integracion_cuenta
            WHERE company_id = @CompanyId AND servicio_id IS NULL",
            new { CompanyId }, Transaction))).ToHashSet();

        var esperados = new[]
        {
            "CXC", "INGRESO", "CAJA", "BANCO_DEFAULT", "ISV", "DESCUENTO",
            "RECARGO_MORA", "PREVISION_INCOBRABLE", "GASTO_INCOBRABLE",
            "RESULTADO_EJERCICIO", "RESULTADO_ACUMULADO", "TRANSITORIA"
        };
        foreach (var uso in esperados)
        {
            Assert.Contains(uso, usosGenerales);
        }
        Assert.DoesNotContain("DEVOLUCION_NC", usosGenerales);

        // La matriz analítica (servicio × categoría) existe y CxC espeja a
        // Ingresos fila por fila en dimensiones.
        var conteos = await Connection.QueryFirstAsync<(long ingreso, long cxc, long prevision)>(new CommandDefinition(@"
            SELECT
                count(*) FILTER (WHERE uso = 'INGRESO' AND categoria_servicio_id IS NOT NULL),
                count(*) FILTER (WHERE uso = 'CXC' AND categoria_servicio_id IS NOT NULL),
                count(*) FILTER (WHERE uso = 'PREVISION_INCOBRABLE' AND categoria_servicio_id IS NOT NULL)
            FROM public.con_integracion_cuenta
            WHERE company_id = @CompanyId",
            new { CompanyId }, Transaction));

        Assert.True(conteos.ingreso > 0, "El perfil no generó matriz analítica de INGRESO.");
        Assert.Equal(conteos.ingreso, conteos.cxc);
        Assert.Equal(conteos.ingreso, conteos.prevision);
    }

    [SkippableFact]
    public async Task Perfil_ERSAPS_es_idempotente()
    {
        var primera = await AplicarPerfilDesdeCeroAsync();

        var segunda = await Connection.QueryFirstAsync<PerfilResult>(new CommandDefinition(
            "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'ERSAPS', 'test')",
            new { CompanyId }, Transaction));

        Assert.Equal(0, segunda.insertadas);
        Assert.Equal(primera.insertadas, segunda.existentes);
    }

    [SkippableFact]
    public async Task Perfil_desconocido_es_rechazado()
    {
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(
                "SELECT * FROM public.sp_con_aplicar_perfil_integracion(@CompanyId, 'NIIF', 'test')",
                new { CompanyId }, Transaction)));

        Assert.Contains("no soportado", ex.MessageText);
    }

    // ------------------------------------------------------------------
    // fn_con_resolver_cuenta — los 3 modos de granularidad
    // ------------------------------------------------------------------

    [SkippableFact]
    public async Task Resuelve_modo_general_sin_dimensiones()
    {
        await AplicarPerfilDesdeCeroAsync();

        Assert.Equal("11101020000", await ResolverCodigoAsync("CAJA"));          // Caja General
        Assert.Equal("31502000000", await ResolverCodigoAsync("RESULTADO_EJERCICIO"));
    }

    [SkippableFact]
    public async Task Resuelve_modo_por_servicio()
    {
        await AplicarPerfilDesdeCeroAsync();
        var conexion = await ServicioIdAsync("CONEXION");
        Skip.If(conexion is null, "No existe el servicio CONEXION en la BD de prueba.");

        Assert.Equal("51301000000", await ResolverCodigoAsync("INGRESO", conexion));  // Cargo por Conexión
        Assert.Equal("11301010301", await ResolverCodigoAsync("CXC", conexion));      // espejo CxC
    }

    [SkippableFact]
    public async Task Resuelve_modo_servicio_categoria_medicion()
    {
        await AplicarPerfilDesdeCeroAsync();
        var agua = await ServicioIdAsync("AGUA_POTABLE");
        var comercial = await CategoriaIdAsync("comercial%");
        Skip.If(agua is null || comercial is null, "Faltan AGUA_POTABLE o categoría Comercial.");

        // Agua × Comercial: con medición = 5.1.1.02, sin medición = 5.1.1.06.
        Assert.Equal("51102000000", await ResolverCodigoAsync("INGRESO", agua, comercial, conMedicion: true));
        Assert.Equal("51106000000", await ResolverCodigoAsync("INGRESO", agua, comercial, conMedicion: false));
        Assert.Equal("11301010106", await ResolverCodigoAsync("CXC", agua, comercial, conMedicion: false));
    }

    [SkippableFact]
    public async Task Fallback_especifico_a_servicio_a_general()
    {
        await AplicarPerfilDesdeCeroAsync();
        var agua = await ServicioIdAsync("AGUA_POTABLE");
        Skip.If(agua is null, "No existe AGUA_POTABLE en la BD de prueba.");

        // Sin categoría ni medición cae a la fila del servicio (representativa).
        Assert.Equal("51101000000", await ResolverCodigoAsync("INGRESO", agua));

        // Servicio nuevo sin filas en la matriz cae a la fila general.
        var nuevo = await Connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            INSERT INTO public.adm_servicio
                (company_id, tipo_servicio_id, codigo, nombre, status_id, created_by)
            SELECT @CompanyId, tipo_servicio_id, 'TEST_F1_SIN_MATRIZ', 'Servicio test F1', 1, 'TEST'
            FROM public.adm_tipo_servicio
            WHERE company_id = @CompanyId
            LIMIT 1
            RETURNING servicio_id",
            new { CompanyId }, Transaction));

        Assert.Equal("51304000000", await ResolverCodigoAsync("INGRESO", nuevo));  // Otros Cargos (general)

        // Categoría desconocida con medición desconocida sobre agua: gana la
        // fila del servicio, no la general.
        Assert.Equal("51101000000", await ResolverCodigoAsync("INGRESO", agua, categoriaId: null, conMedicion: null));
    }

    [SkippableFact]
    public async Task Sin_configuracion_lanza_excepcion_clara()
    {
        await AplicarPerfilDesdeCeroAsync();

        // DEVOLUCION_NC queda sin configurar por diseño.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteScalarAsync<long>(new CommandDefinition(
                "SELECT public.fn_con_resolver_cuenta(@CompanyId, 'DEVOLUCION_NC')",
                new { CompanyId }, Transaction)));

        Assert.Contains("no hay cuenta configurada", ex.MessageText);
        Assert.Contains("DEVOLUCION_NC", ex.MessageText);
    }

    // ------------------------------------------------------------------
    // Unicidad tenant-safe (índice de expresiones con comodines NULL)
    // ------------------------------------------------------------------

    [SkippableFact]
    public async Task Unicidad_no_permite_dos_comodines_iguales()
    {
        await AplicarPerfilDesdeCeroAsync();

        // Ya existe la fila general de CAJA; otra fila general del mismo uso
        // debe violar ux_con_integracion_cuenta_dims aunque las dimensiones
        // sean NULL.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_cuenta (company_id, uso, account_id, created_by)
                SELECT @CompanyId, 'CAJA', account_id, 'TEST'
                FROM public.con_plan_cuentas
                WHERE company_id = @CompanyId AND allows_posting
                LIMIT 1",
                new { CompanyId }, Transaction)));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, ex.SqlState);
    }

    [SkippableFact]
    public async Task Categoria_sin_servicio_es_rechazada()
    {
        await AplicarPerfilDesdeCeroAsync();

        var categoria = await CategoriaIdAsync("%");
        Skip.If(categoria is null, "No hay categorías de servicio en la BD de prueba.");

        // ck_con_integracion_cuenta_dims: categoría/medición requieren servicio.
        var ex = await Assert.ThrowsAsync<PostgresException>(() =>
            Connection.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.con_integracion_cuenta
                    (company_id, uso, categoria_servicio_id, account_id, created_by)
                SELECT @CompanyId, 'DEVOLUCION_NC', @Categoria, account_id, 'TEST'
                FROM public.con_plan_cuentas
                WHERE company_id = @CompanyId AND allows_posting
                LIMIT 1",
                new { CompanyId, Categoria = categoria }, Transaction)));

        Assert.Equal(PostgresErrorCodes.CheckViolation, ex.SqlState);
    }
}
