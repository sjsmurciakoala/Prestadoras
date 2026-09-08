using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.ActivosFijos;

/// <summary>
/// <c>sp_af_activo_guardar</c>: el corazón del registro de activos fijos. Cubre lo que la
/// pantalla NO valida y por lo tanto solo se puede probar contra la base:
/// <list type="bullet">
///   <item>la herencia del tipo (vida útil, método, residual y cuentas contables);</item>
///   <item>el código autogenerado con el prefijo del tipo y su correlativo;</item>
///   <item>las ocho validaciones que terminan en <c>RAISE EXCEPTION</c>;</item>
///   <item>los derivados de línea recta: cuota mensual, cuota diaria y fin de vida útil.</item>
/// </list>
/// Todo dentro del <c>BEGIN … ROLLBACK</c> del harness.
/// </summary>
[Collection("Postgres")]
public sealed class ActivoFijoGuardarTests : ActivosFijosTestBase
{
    public ActivoFijoGuardarTests(PostgresFixture fixture) : base(fixture)
    {
    }

    // ═══════════════════════════════════════════════ Herencia del tipo

    /// <summary>
    /// El activo entra sin vida útil, sin residual, sin método y sin cuentas: todo eso se lo
    /// presta el tipo. Es la razón de ser de <c>af_tipo_activo</c>.
    /// </summary>
    [SkippableFact]
    public async Task Activo_sin_datos_propios_hereda_vida_metodo_residual_y_cuentas_del_tipo()
    {
        var tipoId = await CrearTipoAsync(
            vidaUtilAnios: 5m,
            porcentajeResidual: 10m,
            cuentaActivo: "1201",
            cuentaDepreciacionAcumulada: "1290",
            cuentaGastoDepreciacion: "5101",
            cuentaPerdidaBaja: "5199");

        var (id, _) = await GuardarActivoAsync(tipoActivoId: tipoId, valorCompra: 50_000m, depreciar: true);
        var activo = await LeerActivoAsync(id);

        Assert.Equal(5m, activo.vida_util_anios);
        Assert.Equal(MetodoLineaRecta, activo.metodo_depreciacion_id);
        Assert.Equal(5_000m, activo.valor_rescate);        // 10 % de 50,000
        // OJO: pese al nombre, valor_a_depreciar guarda la CUOTA ANUAL, no la base.
        // Es la semantica del historico de SIMAFI (827 de 829 filas cumplen
        // valor_a_depreciar = depreciacion_mensual * 12) y el modulo la respeta para que
        // la columna signifique lo mismo en todas las filas. La base depreciable no se
        // almacena: se deriva de valor_compra - valor_rescate.
        Assert.Equal(45_000m, activo.valor_compra - activo.valor_rescate);  // base depreciable
        Assert.Equal(9_000m, activo.valor_a_depreciar);                     // cuota anual: 45,000 / 5

        Assert.Equal("1201", activo.cuenta_contable);
        Assert.Equal("1290", activo.cuenta_depreciacion);
        Assert.Equal("5101", activo.cuenta_gasto);
    }

    /// <summary>Lo que el usuario captura MANDA sobre lo que presta el tipo.</summary>
    [SkippableFact]
    public async Task Activo_con_datos_propios_no_hereda_del_tipo()
    {
        var tipoId = await CrearTipoAsync(
            vidaUtilAnios: 5m,
            porcentajeResidual: 10m,
            cuentaActivo: "1201",
            cuentaDepreciacionAcumulada: "1290",
            cuentaGastoDepreciacion: "5101");

        var (id, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId,
            valorCompra: 50_000m,
            valorRescate: 1_234.56m,
            vidaUtilAnios: 3m,
            metodoDepreciacionId: MetodoLineaRecta,
            depreciar: true,
            cuentaContable: "1301",
            cuentaDepreciacion: "1390",
            cuentaGasto: "5201");

        var activo = await LeerActivoAsync(id);

        Assert.Equal(3m, activo.vida_util_anios);
        Assert.Equal(1_234.56m, activo.valor_rescate);
        Assert.Equal("1301", activo.cuenta_contable);
        Assert.Equal("1390", activo.cuenta_depreciacion);
        Assert.Equal("5201", activo.cuenta_gasto);
    }

    /// <summary>Residual explícito en cero: NO se sustituye por el porcentaje del tipo.</summary>
    [SkippableFact]
    public async Task Activo_con_residual_cero_explicito_no_toma_el_porcentaje_del_tipo()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m, porcentajeResidual: 20m);

        var (id, _) = await GuardarActivoAsync(tipoActivoId: tipoId, valorCompra: 10_000m, valorRescate: 0m);
        var activo = await LeerActivoAsync(id);

        Assert.Equal(0m, activo.valor_rescate);
        Assert.Equal(10_000m, activo.valor_compra - activo.valor_rescate);  // base depreciable
        // Cuota anual = 10,000 / 5 años. El centavo de mas sale de redondear la mensual
        // (166.67 x 12 = 2,000.04): la columna se calcula desde la cuota ya redondeada.
        Assert.Equal(2_000.04m, activo.valor_a_depreciar);
    }

    // ═══════════════════════════════════════════════ Código autogenerado

    /// <summary>
    /// Sin código capturado, la BD arma <c>PREFIJO-000001</c> con el prefijo del tipo y sigue
    /// el correlativo. El regex del generador solo mira códigos con ese patrón, así que los
    /// códigos libres del histórico de SIMAFI no interfieren.
    /// </summary>
    [SkippableFact]
    public async Task Codigo_autogenerado_usa_el_prefijo_del_tipo_y_avanza_el_correlativo()
    {
        var prefijo = "Z" + Sufijo()[..5];
        var tipoId = await CrearTipoAsync(prefijoCodigo: prefijo, vidaUtilAnios: 5m);

        var siguiente = await EscalarAsync<string>(
            "SELECT public.fn_af_activo_siguiente_codigo(@c, @tipo)", new { c = CompanyId, tipo = tipoId });
        Assert.Equal($"{prefijo}-000001", siguiente);

        var (_, primero) = await GuardarActivoAsync(tipoActivoId: tipoId);
        var (_, segundo) = await GuardarActivoAsync(tipoActivoId: tipoId);

        Assert.Equal($"{prefijo}-000001", primero);
        Assert.Equal($"{prefijo}-000002", segundo);

        Assert.Equal($"{prefijo}-000003", await EscalarAsync<string>(
            "SELECT public.fn_af_activo_siguiente_codigo(@c, @tipo)", new { c = CompanyId, tipo = tipoId }));
    }

    /// <summary>Tipo sin prefijo: el correlativo cae al genérico «AF» de la empresa.</summary>
    [SkippableFact]
    public async Task Codigo_autogenerado_sin_prefijo_usa_el_correlativo_generico()
    {
        var tipoId = await CrearTipoAsync(prefijoCodigo: null);

        var esperado = await EscalarAsync<string>(
            "SELECT public.fn_af_activo_siguiente_codigo(@c, @tipo)", new { c = CompanyId, tipo = tipoId });

        var (_, codigo) = await GuardarActivoAsync(tipoActivoId: tipoId);

        Assert.StartsWith("AF-", codigo);
        Assert.Equal(esperado, codigo);
    }

    /// <summary>Con código capturado se respeta el del usuario, normalizado a mayúsculas.</summary>
    [SkippableFact]
    public async Task Codigo_capturado_por_el_usuario_se_guarda_en_mayusculas()
    {
        var tipoId = await CrearTipoAsync(prefijoCodigo: "Z" + Sufijo()[..5]);
        var codigo = "ZAC-" + Sufijo();

        var (id, devuelto) = await GuardarActivoAsync(tipoActivoId: tipoId, codigo: "  " + codigo.ToLowerInvariant() + " ");

        Assert.Equal(codigo, devuelto);
        Assert.Equal(codigo, (await LeerActivoAsync(id)).codigo_activo);
    }

    // ═══════════════════════════════════════════════ Derivados de línea recta

    /// <summary>
    /// Cuota mensual = depreciable / (años × 12); cuota diaria = depreciable / (años × 365);
    /// fin de vida útil = inicio + periodos meses. Los tres los escribe la BD, no el usuario.
    /// </summary>
    [SkippableFact]
    public async Task Derivados_de_linea_recta_cuota_mensual_diaria_y_fin_de_vida_util()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var (id, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId,
            fechaCompra: new DateOnly(2024, 1, 15),
            valorCompra: 100_000m,
            valorRescate: 10_000m,
            vidaUtilAnios: 5m,
            depreciar: true);

        var activo = await LeerActivoAsync(id);

        Assert.Equal(90_000m, activo.valor_compra - activo.valor_rescate);  // base depreciable
        Assert.Equal(18_000m, activo.valor_a_depreciar);                     // cuota anual: 90,000 / 5
        Assert.Equal(60m, activo.vida_util_periodos);
        Assert.Equal(1_500m, activo.depreciacion_mensual);   // 90,000 / 60
        Assert.Equal(49.32m, activo.depreciacion_diaria);    // 90,000 / 1,825 redondeado
        Assert.Equal("2024-01-15", activo.fecha_compra);
        Assert.Equal("2024-01-15", activo.fecha_inicio_depreciacion); // sin captura, = la compra
        Assert.Equal("2029-01-15", activo.fecha_fin_depreciacion);
        Assert.Equal(100_000m, activo.valor_libros);         // sin depreciación acumulada
    }

    /// <summary>
    /// El activo comprado en diciembre que entra en servicio después: la vida útil corre
    /// desde la fecha de inicio, no desde la compra.
    /// </summary>
    [SkippableFact]
    public async Task Fecha_de_inicio_posterior_a_la_compra_corre_el_fin_de_vida_util()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 2m);

        var (id, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId,
            fechaCompra: new DateOnly(2024, 1, 15),
            fechaInicioDepreciacion: new DateOnly(2024, 3, 1),
            valorCompra: 24_000m,
            valorRescate: 0m,
            vidaUtilAnios: 2m,
            depreciar: true);

        var activo = await LeerActivoAsync(id);

        Assert.Equal("2024-03-01", activo.fecha_inicio_depreciacion);
        Assert.Equal("2026-03-01", activo.fecha_fin_depreciacion);
        Assert.Equal(1_000m, activo.depreciacion_mensual);   // 24,000 / 24
    }

    /// <summary>Sin vida útil y sin depreciar no hay cuotas ni fin de vida: quedan en cero y nulo.</summary>
    [SkippableFact]
    public async Task Activo_sin_vida_util_y_sin_depreciar_no_calcula_cuotas_ni_fin()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: null);

        var (id, _) = await GuardarActivoAsync(tipoActivoId: tipoId, valorCompra: 8_000m, depreciar: false);
        var activo = await LeerActivoAsync(id);

        Assert.Null(activo.vida_util_anios);
        Assert.Null(activo.fecha_fin_depreciacion);
        Assert.Equal(0m, activo.depreciacion_mensual);
        Assert.Equal(0m, activo.depreciacion_diaria);
        Assert.False(activo.depreciar);
    }

    /// <summary>
    /// La depreciación acumulada de arranque baja el valor en libros y se escribe en las DOS
    /// columnas (<c>depreciacion_acumulada</c> y <c>valor_depreciado</c>), porque el histórico
    /// de SIMAFI dejó el acumulado en la segunda y las lecturas toman el mayor de ambas.
    /// </summary>
    [SkippableFact]
    public async Task Depreciacion_acumulada_de_arranque_baja_el_valor_en_libros()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var (id, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 100_000m, valorRescate: 10_000m,
            vidaUtilAnios: 5m, depreciar: true, depreciacionAcumulada: 30_000m);

        var activo = await LeerActivoAsync(id);

        Assert.Equal(30_000m, activo.depreciacion_acumulada);
        Assert.Equal(30_000m, activo.valor_depreciado);
        Assert.Equal(70_000m, activo.valor_libros);
    }

    // ═══════════════════════════════════════════════ Validaciones (RAISE EXCEPTION)

    [SkippableFact]
    public async Task Valor_residual_igual_o_mayor_que_la_compra_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        Assert.Contains("valor residual", await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 1_000m, valorRescate: 1_000m)));

        Assert.Contains("valor residual", await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 1_000m, valorRescate: 1_500m)));

        Assert.Contains("no puede ser negativo", await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 1_000m, valorRescate: -1m)));
    }

    /// <summary>
    /// La acumulada nunca puede pasarse del valor depreciable (compra − residual); justo en
    /// el tope sí se acepta: es el activo totalmente depreciado.
    /// </summary>
    [SkippableFact]
    public async Task Depreciacion_acumulada_mayor_que_el_valor_depreciable_es_rechazada()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 1_000m, valorRescate: 200m, depreciacionAcumulada: 800.01m));
        Assert.Contains("no puede superar el valor depreciable", error);

        Assert.Contains("no puede ser negativa", await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 1_000m, valorRescate: 200m, depreciacionAcumulada: -1m)));

        // En el tope exacto sí entra: activo totalmente depreciado, en libros queda el residual.
        var (id, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 1_000m, valorRescate: 200m, depreciacionAcumulada: 800m);
        Assert.Equal(200m, (await LeerActivoAsync(id)).valor_libros);
    }

    [SkippableFact]
    public async Task Fecha_de_compra_futura_es_rechazada()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, fechaCompra: Hoy(1)));

        Assert.Contains("fecha de compra no puede ser futura", error);
    }

    [SkippableFact]
    public async Task La_depreciacion_no_puede_empezar_antes_de_la_compra()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, fechaCompra: Hoy(-10), fechaInicioDepreciacion: Hoy(-20)));

        Assert.Contains("no puede empezar antes de la fecha de compra", error);
    }

    /// <summary>
    /// «Fuera de servicio» no se deprecia (<c>permite_depreciar = false</c>): con la casilla
    /// marcada la BD lo rechaza; con la casilla apagada el mismo activo entra sin problema.
    /// </summary>
    [SkippableFact]
    public async Task Estado_que_no_permite_depreciar_con_la_casilla_marcada_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, estadoActivoId: EstadoFueraDeServicio, vidaUtilAnios: 5m, depreciar: true));

        Assert.Contains("no se deprecia", error);
        Assert.Contains("Fuera de servicio", error);

        var (id, _) = await GuardarActivoAsync(
            tipoActivoId: tipoId, estadoActivoId: EstadoFueraDeServicio, vidaUtilAnios: 5m, depreciar: false);

        var activo = await LeerActivoAsync(id);
        Assert.False(activo.depreciar);
        Assert.Equal(EstadoFueraDeServicio, activo.estado_activo_id);
    }

    [SkippableFact]
    public async Task Codigo_de_activo_duplicado_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var codigo = "ZAC-" + Sufijo();

        await GuardarActivoAsync(tipoActivoId: tipoId, codigo: codigo);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, codigo: codigo));

        Assert.Contains("Ya existe un activo con el código", error);
        Assert.Contains(codigo, error);
    }

    /// <summary>
    /// La placa de inventario identifica al activo: única por empresa. Se compara ya
    /// normalizada, así que capturarla en minúsculas tampoco cuela.
    /// </summary>
    [SkippableFact]
    public async Task Codigo_de_barras_duplicado_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        var barra = "ZBR" + Sufijo();

        await GuardarActivoAsync(tipoActivoId: tipoId, codigoBarra: barra);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(
            tipoActivoId: tipoId, codigoBarra: barra.ToLowerInvariant()));

        Assert.Contains("Ya existe un activo con el código de barras", error);
    }

    /// <summary>Dos activos SIN código de barras conviven: el índice único ignora los nulos.</summary>
    [SkippableFact]
    public async Task Dos_activos_sin_codigo_de_barras_conviven()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var (primero, _) = await GuardarActivoAsync(tipoActivoId: tipoId, codigoBarra: "   ");
        var (segundo, _) = await GuardarActivoAsync(tipoActivoId: tipoId, codigoBarra: null);

        Assert.Null((await LeerActivoAsync(primero)).codigo_barra);
        Assert.Null((await LeerActivoAsync(segundo)).codigo_barra);
    }

    [SkippableFact]
    public async Task Campos_obligatorios_descripcion_tipo_estado_valor_y_fecha()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        Assert.Contains("descripción del activo es obligatoria",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, descripcion: "   ")));

        Assert.Contains("Debe seleccionar el tipo de activo",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: null)));

        Assert.Contains("tipo de activo seleccionado no existe",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: -1)));

        Assert.Contains("Debe seleccionar el estado del activo",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, estadoActivoId: null)));

        Assert.Contains("estado del activo seleccionado no existe",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, estadoActivoId: 99)));

        Assert.Contains("valor de compra debe ser mayor que cero",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, valorCompra: 0m)));

        Assert.Contains("fecha de compra es obligatoria",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, fechaCompraNula: true)));
    }

    [SkippableFact]
    public async Task Tipo_inactivo_no_admite_activos_nuevos()
    {
        var tipoId = await CrearTipoAsync(nombre: "TIPO RETIRADO", vidaUtilAnios: 5m, activo: false);

        var error = await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId));

        Assert.Contains("está inactivo y no admite registros nuevos", error);
        Assert.Contains("TIPO RETIRADO", error);
    }

    /// <summary>
    /// Las tres referencias que el SP valida a mano porque no tienen FK física
    /// (empleado y proveedor) o porque la FK no distingue empresa (ubicación).
    /// </summary>
    [SkippableFact]
    public async Task Ubicacion_empleado_y_proveedor_inexistentes_son_rechazados()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        Assert.Contains("ubicación seleccionada no existe",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, ubicacionId: -1)));

        Assert.Contains("responsable seleccionado no existe",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, empleadoId: -1)));

        Assert.Contains("proveedor seleccionado no existe",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: tipoId, codProveedor: "ZZNOEXISTE")));
    }

    [SkippableFact]
    public async Task Depreciar_sin_vida_util_o_con_metodo_no_implementado_es_rechazado()
    {
        var sinVida = await CrearTipoAsync(vidaUtilAnios: null);
        Assert.Contains("necesita una vida útil mayor que cero",
            await ErrorDeAsync(() => GuardarActivoAsync(tipoActivoId: sinVida, depreciar: true)));

        var conVida = await CrearTipoAsync(vidaUtilAnios: 5m);
        Assert.Contains("todavía no está disponible",
            await ErrorDeAsync(() => GuardarActivoAsync(
                tipoActivoId: conVida, depreciar: true, metodoDepreciacionId: MetodoSaldoDecreciente)));
    }

    // ═══════════════════════════════════════════════ Edición y estados finales

    /// <summary>Al editar sin mandar código se conserva el que ya tenía y se recalculan los derivados.</summary>
    [SkippableFact]
    public async Task Editar_conserva_el_codigo_y_recalcula_los_derivados()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var (id, codigo) = await GuardarActivoAsync(
            tipoActivoId: tipoId, valorCompra: 10_000m, vidaUtilAnios: 5m, depreciar: true);

        var (mismoId, mismoCodigo) = await GuardarActivoAsync(
            id: id, codigo: null, descripcion: "ACTIVO EDITADO", tipoActivoId: tipoId,
            valorCompra: 20_000m, valorRescate: 0m, vidaUtilAnios: 4m, depreciar: true);

        Assert.Equal(id, mismoId);
        Assert.Equal(codigo, mismoCodigo);

        var activo = await LeerActivoAsync(id);
        Assert.Equal("ACTIVO EDITADO", activo.descripcion);
        Assert.Equal(20_000m, activo.valor_compra);
        Assert.Equal(4m, activo.vida_util_anios);
        Assert.Equal(48m, activo.vida_util_periodos);
        Assert.Equal(416.67m, activo.depreciacion_mensual);   // 20,000 / 48
    }

    [SkippableFact]
    public async Task Editar_un_activo_inexistente_es_rechazado()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        Assert.Contains("No se encontró el activo",
            await ErrorDeAsync(() => GuardarActivoAsync(
                id: IdInexistente, codigo: "ZAC-" + Sufijo(), tipoActivoId: tipoId)));
    }

    /// <summary>
    /// Los estados finales encienden las banderas heredadas de SIMAFI (descargado y vendido),
    /// que es lo que siguen leyendo los reportes viejos.
    /// </summary>
    [SkippableFact]
    public async Task Estado_final_enciende_la_bandera_de_descargado_o_de_vendido()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);

        var (baja, _) = await GuardarActivoAsync(tipoActivoId: tipoId, estadoActivoId: EstadoDadoDeBaja);
        var (venta, _) = await GuardarActivoAsync(tipoActivoId: tipoId, estadoActivoId: EstadoVendido);

        var dadoDeBaja = await LeerActivoAsync(baja);
        Assert.True(dadoDeBaja.descargado);
        Assert.False(dadoDeBaja.vendido);

        var vendido = await LeerActivoAsync(venta);
        Assert.False(vendido.descargado);
        Assert.True(vendido.vendido);
    }

    // ═══════════════════════════════════════════════ Listado y resumen

    /// <summary>
    /// El activo nuevo sale completo en el listado (con el nombre del tipo y del estado, no
    /// sus ids) y suma en las tarjetas de resumen de la pantalla.
    /// </summary>
    [SkippableFact]
    public async Task Listado_y_resumen_reflejan_el_activo_nuevo()
    {
        var tipoId = await CrearTipoAsync(nombre: "EQUIPO DE COMPUTO", vidaUtilAnios: 4m);
        var antes = await LeerResumenAsync();

        var (id, codigo) = await GuardarActivoAsync(
            tipoActivoId: tipoId, descripcion: "LAPTOP DE PRUEBA", valorCompra: 25_000m,
            valorRescate: 0m, vidaUtilAnios: 4m, depreciar: true, depreciacionAcumulada: 5_000m,
            responsable: "JUAN PEREZ");

        var fila = Assert.Single(await ListarAsync<ListadoRow>(@"
            SELECT id, codigo_activo, descripcion, tipo_activo, estado_activo, estado_es_final,
                   responsable, valor_compra, valor_rescate, depreciacion_acumulada, valor_libros,
                   depreciar, pendiente_completar
              FROM public.fn_af_activo_listar(p_company_id => @c, p_tipo_id => @tipo)",
            new { c = CompanyId, tipo = tipoId }));

        Assert.Equal(id, fila.id);
        Assert.Equal(codigo, fila.codigo_activo);
        Assert.Equal("LAPTOP DE PRUEBA", fila.descripcion);
        Assert.Equal("EQUIPO DE COMPUTO", fila.tipo_activo);   // el nombre, nunca el id
        Assert.Equal("En uso", fila.estado_activo);
        Assert.False(fila.estado_es_final);
        Assert.Equal("JUAN PEREZ", fila.responsable);
        Assert.Equal(5_000m, fila.depreciacion_acumulada);
        Assert.Equal(20_000m, fila.valor_libros);
        Assert.False(fila.pendiente_completar);                // trae tipo y estado

        var despues = await LeerResumenAsync();
        Assert.Equal(antes.total_activos + 1, despues.total_activos);
        Assert.Equal(antes.en_patrimonio + 1, despues.en_patrimonio);
        Assert.Equal(antes.valor_compra + 25_000m, despues.valor_compra);
        Assert.Equal(antes.depreciacion_acumulada + 5_000m, despues.depreciacion_acumulada);
        Assert.Equal(antes.pendientes_completar, despues.pendientes_completar);
    }

    /// <summary>
    /// El filtro «pendientes de completar» es para el histórico de SIMAFI: filas sin tipo o
    /// sin estado. Un registro nuevo nunca puede quedar así, porque el SP los exige.
    /// </summary>
    [SkippableFact]
    public async Task Filtro_de_pendientes_no_devuelve_registros_nuevos()
    {
        var tipoId = await CrearTipoAsync(vidaUtilAnios: 5m);
        await GuardarActivoAsync(tipoActivoId: tipoId);

        var pendientes = await ListarAsync<ListadoRow>(@"
            SELECT id, codigo_activo, descripcion, tipo_activo, estado_activo, estado_es_final,
                   responsable, valor_compra, valor_rescate, depreciacion_acumulada, valor_libros,
                   depreciar, pendiente_completar
              FROM public.fn_af_activo_listar(p_company_id => @c, p_tipo_id => @tipo, p_pendientes => TRUE)",
            new { c = CompanyId, tipo = tipoId });

        Assert.Empty(pendientes);
    }

    private sealed class ListadoRow
    {
        public int id { get; set; }
        public string codigo_activo { get; set; } = string.Empty;
        public string descripcion { get; set; } = string.Empty;
        public string? tipo_activo { get; set; }
        public string? estado_activo { get; set; }
        public bool estado_es_final { get; set; }
        public string? responsable { get; set; }
        public decimal valor_compra { get; set; }
        public decimal valor_rescate { get; set; }
        public decimal depreciacion_acumulada { get; set; }
        public decimal valor_libros { get; set; }
        public bool depreciar { get; set; }
        public bool pendiente_completar { get; set; }
    }
}
