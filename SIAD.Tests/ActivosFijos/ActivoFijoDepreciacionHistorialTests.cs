using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.ActivosFijos;

/// <summary>
/// Historial de depreciación por activo (<c>Database/2026-09-08_af_historial_depreciacion.sql</c>):
/// las dos funciones de lectura que alimentan la pestaña de la ficha sobre las 44,579 filas
/// migradas de SIMAFI.
/// <para>
/// Dos rarezas del dato de origen, y las dos están probadas aquí porque son la razón de ser de
/// las funciones: (1) la migración dejó filas con <c>activo_fijo_id</c> en NULL que solo se
/// encuentran por <c>codigo_activo</c>, y el listado las marca con <c>vinculo_por_codigo</c>;
/// (2) el histórico guarda el acumulado en <c>valor_depreciado</c> y deja
/// <c>depreciacion_acumulada</c> en cero, así que TODAS las lecturas del módulo toman el mayor
/// de las dos columnas.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class ActivoFijoDepreciacionHistorialTests : ActivosFijosTestBase
{
    public ActivoFijoDepreciacionHistorialTests(PostgresFixture fixture) : base(fixture)
    {
    }

    private Task<List<DepreciacionRow>> LeerHistorialAsync(int activoId)
        => ListarAsync<DepreciacionRow>(@"
            SELECT id, anio, mes, periodo, fecha_depreciacion::text AS fecha_depreciacion,
                   valor_depreciado, valor_neto_libros, cuenta_depreciacion, cuenta_gasto,
                   descripcion, vinculo_por_codigo
              FROM public.fn_af_activo_depreciacion_listar(@c, @activo)",
            new { c = CompanyId, activo = activoId });

    private Task<List<ResumenDepreciacionRow>> LeerResumenHistorialAsync(int activoId)
        => ListarAsync<ResumenDepreciacionRow>(
            "SELECT * FROM public.fn_af_activo_depreciacion_resumen(@c, @activo)",
            new { c = CompanyId, activo = activoId });

    /// <summary>
    /// El detalle se resuelve por las DOS vías —id y código— y cada fila dice de cuál salió.
    /// Sin la vía del código se perderían las 792 filas que la migración dejó sueltas.
    /// </summary>
    [SkippableFact]
    public async Task Historial_lista_el_detalle_vinculado_por_id_y_por_codigo()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, codigo) = await GuardarActivoAsync(tipoActivoId: tipoId, valorCompra: 100_000m);

        await SembrarDepreciacionAsync(activoId, codigo, 2025, 6, 1_000m, valorNetoLibros: 90_000m);
        await SembrarDepreciacionAsync(null, codigo, 2024, 1, 500m, valorNetoLibros: 95_000m);

        var historial = await LeerHistorialAsync(activoId);
        Assert.Equal(2, historial.Count);

        var reciente = historial[0];   // ordena por año y mes descendente
        Assert.Equal("2025-06", reciente.periodo);
        Assert.Equal(1_000m, reciente.valor_depreciado);
        Assert.Equal(90_000m, reciente.valor_neto_libros);
        Assert.Equal("2025-06-01", reciente.fecha_depreciacion);
        Assert.False(reciente.vinculo_por_codigo);

        var antigua = historial[1];
        Assert.Equal("2024-01", antigua.periodo);
        Assert.Equal(500m, antigua.valor_depreciado);
        Assert.True(antigua.vinculo_por_codigo);   // la migración la dejó sin id
    }

    /// <summary>
    /// Las filas que el origen trajo con año cero se rotulan «Sin período»: se muestran, no
    /// se esconden.
    /// </summary>
    [SkippableFact]
    public async Task Historial_rotula_sin_periodo_las_filas_sin_anio()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, codigo) = await GuardarActivoAsync(tipoActivoId: tipoId);

        await SembrarDepreciacionAsync(activoId, codigo, 0, 0, 250m);

        var fila = Assert.Single(await LeerHistorialAsync(activoId));
        Assert.Equal("Sin período", fila.periodo);
        Assert.Null(fila.fecha_depreciacion);
        Assert.Equal(250m, fila.valor_depreciado);
    }

    [SkippableFact]
    public async Task Historial_no_trae_filas_de_otro_activo()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (primero, codigoPrimero) = await GuardarActivoAsync(tipoActivoId: tipoId);
        var (segundo, codigoSegundo) = await GuardarActivoAsync(tipoActivoId: tipoId);

        await SembrarDepreciacionAsync(primero, codigoPrimero, 2025, 1, 100m);
        await SembrarDepreciacionAsync(segundo, codigoSegundo, 2025, 1, 200m);
        // Huérfana de un código que no existe en el maestro: no le toca a nadie.
        await SembrarDepreciacionAsync(null, "ZHUERFANO-" + Sufijo(), 2025, 1, 999m);

        Assert.Equal(100m, Assert.Single(await LeerHistorialAsync(primero)).valor_depreciado);
        Assert.Equal(200m, Assert.Single(await LeerHistorialAsync(segundo)).valor_depreciado);
    }

    /// <summary>
    /// El resumen enfrenta el detalle con lo que declara el maestro y devuelve la diferencia:
    /// el descuadre se ve en la ficha en vez de quedar escondido (es requisito de la Fase 2,
    /// porque el motor necesita saber desde dónde continuar).
    /// </summary>
    [SkippableFact]
    public async Task Resumen_suma_el_detalle_y_lo_contrasta_con_el_acumulado_del_maestro()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, codigo) = await GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 100_000m, valorRescate: 0m,
            vidaUtilAnios: 5m, depreciar: true, depreciacionAcumulada: 30_000m);

        await SembrarDepreciacionAsync(activoId, codigo, 2024, 12, 12_000m);
        await SembrarDepreciacionAsync(activoId, codigo, 2025, 12, 8_000m);

        var resumen = Assert.Single(await LeerResumenHistorialAsync(activoId));

        Assert.Equal(2L, resumen.filas);
        Assert.Equal((short)2024, resumen.anio_desde);
        Assert.Equal((short)2025, resumen.anio_hasta);
        Assert.Equal(20_000m, resumen.total_detalle);
        Assert.Equal(30_000m, resumen.acumulada_maestro);
        Assert.Equal(10_000m, resumen.diferencia);        // el detalle no explica todo
        Assert.Equal(100_000m, resumen.valor_compra);
        Assert.Equal(70_000m, resumen.valor_libros);
    }

    /// <summary>Sin detalle el resumen no rompe: ceros y la diferencia es todo el acumulado.</summary>
    [SkippableFact]
    public async Task Resumen_sin_detalle_devuelve_ceros_y_la_diferencia_es_el_acumulado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 100_000m, valorRescate: 0m,
            vidaUtilAnios: 5m, depreciar: true, depreciacionAcumulada: 30_000m);

        var resumen = Assert.Single(await LeerResumenHistorialAsync(activoId));

        Assert.Equal(0L, resumen.filas);
        Assert.Null(resumen.anio_desde);
        Assert.Null(resumen.anio_hasta);
        Assert.Equal(0m, resumen.total_detalle);
        Assert.Equal(30_000m, resumen.diferencia);
    }

    /// <summary>
    /// La regla del histórico: cuando <c>depreciacion_acumulada</c> viene en cero y el dato
    /// real está en <c>valor_depreciado</c> (las 829 filas migradas), las lecturas del módulo
    /// tienen que devolver el MAYOR de las dos. Leer solo la primera mostraría cero
    /// depreciación sobre L. 16 millones ya depreciados.
    /// </summary>
    [SkippableFact]
    public async Task Lecturas_toman_el_mayor_entre_acumulada_y_valor_depreciado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var (activoId, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 100_000m, valorRescate: 0m,
            vidaUtilAnios: 5m, depreciar: true, depreciacionAcumulada: 30_000m);

        // Se le da la forma del dato migrado de SIMAFI.
        await EjecutarAsync(@"
            UPDATE public.af_activo_fijo SET depreciacion_acumulada = 0
             WHERE company_id = @c AND id = @id",
            new { c = CompanyId, id = activoId });

        var resumen = Assert.Single(await LeerResumenHistorialAsync(activoId));
        Assert.Equal(30_000m, resumen.acumulada_maestro);

        var enElListado = await EscalarAsync<decimal>(@"
            SELECT depreciacion_acumulada
              FROM public.fn_af_activo_listar(p_company_id => @c, p_tipo_id => @tipo)",
            new { c = CompanyId, tipo = tipoId });
        Assert.Equal(30_000m, enElListado);
    }

    [SkippableFact]
    public async Task Historial_de_un_activo_inexistente_no_devuelve_nada()
    {
        Assert.Empty(await LeerHistorialAsync(IdInexistente));
        Assert.Empty(await LeerResumenHistorialAsync(IdInexistente));
    }

    private sealed class DepreciacionRow
    {
        public int id { get; set; }
        public short anio { get; set; }
        public short mes { get; set; }
        public string periodo { get; set; } = string.Empty;
        public string? fecha_depreciacion { get; set; }
        public decimal valor_depreciado { get; set; }
        public decimal valor_neto_libros { get; set; }
        public string? cuenta_depreciacion { get; set; }
        public string? cuenta_gasto { get; set; }
        public string? descripcion { get; set; }
        public bool vinculo_por_codigo { get; set; }
    }

    private sealed class ResumenDepreciacionRow
    {
        public long filas { get; set; }
        public short? anio_desde { get; set; }
        public short? anio_hasta { get; set; }
        public decimal total_detalle { get; set; }
        public decimal acumulada_maestro { get; set; }
        public decimal diferencia { get; set; }
        public decimal valor_compra { get; set; }
        public decimal valor_libros { get; set; }
    }
}
