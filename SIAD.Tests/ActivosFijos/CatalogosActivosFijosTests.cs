using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.ActivosFijos;

/// <summary>
/// Catálogos del módulo Activos Fijos: los dos de sistema (método de depreciación y estado
/// del activo, con ids fijos) y los dos por empresa (<c>af_tipo_activo</c> y
/// <c>af_ubicacion</c>), con sus procedimientos de guardado y desactivación.
/// <para>
/// El tipo es el corazón del módulo: le PRESTA al activo su vida útil, método, porcentaje
/// residual y cuentas contables. La ubicación es jerárquica y el SP tiene que impedir los
/// ciclos, porque un árbol con ciclo deja irrecorrible el CTE recursivo del listado.
/// </para>
/// </summary>
[Collection("Postgres")]
public sealed class CatalogosActivosFijosTests : ActivosFijosTestBase
{
    public CatalogosActivosFijosTests(PostgresFixture fixture) : base(fixture)
    {
    }

    // ═══════════════════════════════════════════════ El módulo está instalado

    [SkippableFact]
    public async Task Tablas_y_rutinas_del_modulo_existen()
    {
        var tablasFaltantes = await ListarAsync<string>(@"
            SELECT o.nombre
              FROM (VALUES ('public.af_metodo_depreciacion'), ('public.af_estado_activo'),
                           ('public.af_tipo_activo'), ('public.af_ubicacion'),
                           ('public.af_activo_fijo'), ('public.af_activo_asignacion'),
                           ('public.af_activo_componente'), ('public.af_activo_fijo_depreciacion')
                   ) AS o(nombre)
             WHERE to_regclass(o.nombre) IS NULL");

        Assert.Empty(tablasFaltantes);

        var rutinasFaltantes = await ListarAsync<string>(@"
            SELECT r.nombre
              FROM (VALUES ('public.sp_af_tipo_activo_guardar'), ('public.sp_af_tipo_activo_desactivar'),
                           ('public.sp_af_ubicacion_guardar'), ('public.sp_af_ubicacion_desactivar'),
                           ('public.sp_af_activo_guardar'), ('public.sp_af_activo_asignar'),
                           ('public.sp_af_activo_componente_guardar'),
                           ('public.sp_af_activo_componente_eliminar'),
                           ('public.fn_af_metodo_depreciacion_listar'), ('public.fn_af_estado_activo_listar'),
                           ('public.fn_af_tipo_activo_listar'), ('public.fn_af_tipo_activo_obtener'),
                           ('public.fn_af_ubicacion_listar'), ('public.fn_af_ubicacion_obtener'),
                           ('public.fn_af_activo_siguiente_codigo'), ('public.fn_af_activo_listar'),
                           ('public.fn_af_activo_obtener'), ('public.fn_af_activo_resumen'),
                           ('public.fn_af_activo_asignacion_listar'),
                           ('public.fn_af_activo_componente_listar'),
                           ('public.fn_af_activo_depreciacion_listar'),
                           ('public.fn_af_activo_depreciacion_resumen')
                   ) AS r(nombre)
             WHERE to_regproc(r.nombre) IS NULL");

        Assert.Empty(rutinasFaltantes);
    }

    /// <summary>
    /// Solo la línea recta está implementada: el resto se ve en el catálogo pero la BD no
    /// deja registrar contra ellos, para no dejar activos que el motor no sabría depreciar.
    /// </summary>
    [SkippableFact]
    public async Task Catalogo_de_metodos_solo_declara_implementada_la_linea_recta()
    {
        var metodos = await ListarAsync<MetodoRow>("SELECT * FROM public.fn_af_metodo_depreciacion_listar()");

        MetodoRow? lineaRecta = null;
        MetodoRow? saldoDecreciente = null;
        foreach (var metodo in metodos)
        {
            if (metodo.id == MetodoLineaRecta) lineaRecta = metodo;
            if (metodo.id == MetodoSaldoDecreciente) saldoDecreciente = metodo;
        }

        Assert.NotNull(lineaRecta);
        Assert.True(lineaRecta!.implementado);
        Assert.Equal("Línea recta", lineaRecta.nombre);

        Assert.NotNull(saldoDecreciente);
        Assert.False(saldoDecreciente!.implementado);
    }

    /// <summary>Las dos banderas que gobiernan el motor: quién se deprecia y quién ya salió.</summary>
    [SkippableFact]
    public async Task Catalogo_de_estados_marca_cual_deprecia_y_cual_es_final()
    {
        var estados = await ListarAsync<EstadoRow>("SELECT * FROM public.fn_af_estado_activo_listar()");

        EstadoRow? enUso = null;
        EstadoRow? fueraDeServicio = null;
        EstadoRow? dadoDeBaja = null;
        foreach (var estado in estados)
        {
            if (estado.id == EstadoEnUso) enUso = estado;
            if (estado.id == EstadoFueraDeServicio) fueraDeServicio = estado;
            if (estado.id == EstadoDadoDeBaja) dadoDeBaja = estado;
        }

        Assert.True(enUso!.permite_depreciar);
        Assert.False(enUso.es_final);

        Assert.False(fueraDeServicio!.permite_depreciar);
        Assert.False(fueraDeServicio.es_final);

        Assert.False(dadoDeBaja!.permite_depreciar);
        Assert.True(dadoDeBaja.es_final);
    }

    // ═══════════════════════════════════════════════ Tipo de activo

    [SkippableFact]
    public async Task Tipo_normaliza_el_codigo_y_el_prefijo_a_mayusculas_sin_espacios()
    {
        var codigo = "ZTA" + Sufijo();
        var prefijo = "Z" + Sufijo()[..5];

        var id = await CrearTipoAsync(
            codigo: "  " + codigo.ToLowerInvariant() + "  ",
            prefijoCodigo: "  " + prefijo.ToLowerInvariant() + " ",
            vidaUtilAnios: 4m,
            porcentajeResidual: 12.5m,
            cuentaActivo: "1201");

        var tipo = await UnicoAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_obtener(@c, @id)", new { c = CompanyId, id });

        Assert.Equal(codigo, tipo.codigo);
        Assert.Equal(prefijo, tipo.prefijo_codigo);
        Assert.Equal(4m, tipo.vida_util_anios);
        Assert.Equal(12.5m, tipo.porcentaje_residual);
        Assert.Equal("1201", tipo.cuenta_activo);
        Assert.True(tipo.activo);
    }

    /// <summary>Prefijo vacío = NULL: el correlativo del activo cae al genérico «AF».</summary>
    [SkippableFact]
    public async Task Tipo_con_prefijo_vacio_lo_guarda_nulo()
    {
        var id = await CrearTipoAsync(prefijoCodigo: "   ");

        var tipo = await UnicoAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_obtener(@c, @id)", new { c = CompanyId, id });

        Assert.Null(tipo.prefijo_codigo);
    }

    [SkippableFact]
    public async Task Tipo_con_codigo_repetido_es_rechazado()
    {
        var codigo = "ZTA" + Sufijo();
        await CrearTipoAsync(codigo: codigo);

        // En minúsculas: la unicidad es sobre el código ya normalizado.
        var error = await ErrorDeAsync(() => CrearTipoAsync(codigo: codigo.ToLowerInvariant()));

        Assert.Contains("Ya existe un tipo de activo", error);
        Assert.Contains(codigo, error);
    }

    [SkippableFact]
    public async Task Tipo_con_metodo_no_implementado_es_rechazado()
    {
        var error = await ErrorDeAsync(() => CrearTipoAsync(metodoDepreciacionId: MetodoSaldoDecreciente));

        Assert.Contains("todavía no está disponible", error);
    }

    [SkippableFact]
    public async Task Tipo_valida_codigo_nombre_vida_util_y_porcentaje_residual()
    {
        Assert.Contains("código del tipo de activo es obligatorio",
            await ErrorDeAsync(() => CrearTipoAsync(codigo: "   ")));

        Assert.Contains("nombre del tipo de activo es obligatorio",
            await ErrorDeAsync(() => CrearTipoAsync(nombre: "  ")));

        Assert.Contains("vida útil debe ser mayor que cero",
            await ErrorDeAsync(() => CrearTipoAsync(vidaUtilAnios: 0m)));

        Assert.Contains("porcentaje residual debe estar entre 0 y 100",
            await ErrorDeAsync(() => CrearTipoAsync(porcentajeResidual: 101m)));

        Assert.Contains("porcentaje residual debe estar entre 0 y 100",
            await ErrorDeAsync(() => CrearTipoAsync(porcentajeResidual: -1m)));
    }

    [SkippableFact]
    public async Task Tipo_se_edita_por_id_y_conserva_el_registro()
    {
        var id = await CrearTipoAsync(nombre: "MOBILIARIO", vidaUtilAnios: 5m);
        var codigoNuevo = "ZTA" + Sufijo();

        var mismoId = await CrearTipoAsync(
            id: id, codigo: codigoNuevo, nombre: "MOBILIARIO Y EQUIPO",
            vidaUtilAnios: 8m, porcentajeResidual: 5m, cuentaActivo: "1205");

        Assert.Equal(id, mismoId);

        var tipo = await UnicoAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_obtener(@c, @id)", new { c = CompanyId, id });

        Assert.Equal(codigoNuevo, tipo.codigo);
        Assert.Equal("MOBILIARIO Y EQUIPO", tipo.nombre);
        Assert.Equal(8m, tipo.vida_util_anios);
        Assert.Equal("1205", tipo.cuenta_activo);
    }

    [SkippableFact]
    public async Task Tipo_inexistente_no_se_edita_ni_se_desactiva()
    {
        Assert.Contains("No se encontró el tipo de activo",
            await ErrorDeAsync(() => CrearTipoAsync(id: IdInexistente)));

        Assert.Contains("No se encontró el tipo de activo",
            await ErrorDeAsync(() => EjecutarAsync(
                "CALL public.sp_af_tipo_activo_desactivar(@c, @id, 'tester')",
                new { c = CompanyId, id = IdInexistente })));
    }

    [SkippableFact]
    public async Task Tipo_desactivado_desaparece_del_listado_de_activos()
    {
        var codigo = "ZTA" + Sufijo();
        var id = await CrearTipoAsync(codigo: codigo);

        await EjecutarAsync("CALL public.sp_af_tipo_activo_desactivar(@c, @id, 'tester')",
            new { c = CompanyId, id });

        var soloActivos = await ListarAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_listar(p_company_id => @c, p_solo_activos => TRUE, p_search => @s)",
            new { c = CompanyId, s = codigo });
        Assert.Empty(soloActivos);

        var soloInactivos = await ListarAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_listar(p_company_id => @c, p_solo_activos => FALSE, p_search => @s)",
            new { c = CompanyId, s = codigo });
        Assert.False(Assert.Single(soloInactivos).activo);
    }

    /// <summary>El listado trae el nombre del método y cuántos activos cuelgan del tipo.</summary>
    [SkippableFact]
    public async Task Tipo_listado_trae_el_metodo_y_cuenta_los_activos_registrados()
    {
        var codigo = "ZTA" + Sufijo();
        var tipoId = await CrearTipoAsync(codigo: codigo, vidaUtilAnios: 5m);

        var sinActivos = Assert.Single(await ListarAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_listar(p_company_id => @c, p_search => @s)",
            new { c = CompanyId, s = codigo }));

        Assert.Equal("Línea recta", sinActivos.metodo_depreciacion);
        Assert.Equal(0L, sinActivos.activos_registrados);

        await GuardarActivoAsync(tipoActivoId: tipoId);
        await GuardarActivoAsync(tipoActivoId: tipoId);

        var conActivos = Assert.Single(await ListarAsync<TipoRow>(
            "SELECT * FROM public.fn_af_tipo_activo_listar(p_company_id => @c, p_search => @s)",
            new { c = CompanyId, s = codigo }));

        Assert.Equal(2L, conActivos.activos_registrados);
    }

    // ═══════════════════════════════════════════════ Ubicación

    [SkippableFact]
    public async Task Ubicacion_hija_arma_la_ruta_con_el_nombre_del_padre()
    {
        var padreId = await CrearUbicacionAsync(nombre: "SEDE CENTRAL");
        var codigoHijo = "ZUB" + Sufijo();
        var hijoId = await CrearUbicacionAsync(codigo: codigoHijo, nombre: "PISO 2", padreId: padreId);

        var hijo = Assert.Single(await ListarAsync<UbicacionRow>(
            "SELECT * FROM public.fn_af_ubicacion_listar(p_company_id => @c, p_search => @s)",
            new { c = CompanyId, s = codigoHijo }));

        Assert.Equal(hijoId, hijo.id);
        Assert.Equal(padreId, hijo.padre_id);
        Assert.Equal("SEDE CENTRAL", hijo.padre_nombre);
        Assert.Equal("SEDE CENTRAL / PISO 2", hijo.ruta);
        Assert.Equal(0L, hijo.activos_registrados);
    }

    [SkippableFact]
    public async Task Ubicacion_cuenta_los_activos_que_la_referencian()
    {
        var codigo = "ZUB" + Sufijo();
        var ubicacionId = await CrearUbicacionAsync(codigo: codigo);
        var tipoId = await CrearTipoAsync();

        await GuardarActivoAsync(tipoActivoId: tipoId, ubicacionId: ubicacionId);

        var fila = Assert.Single(await ListarAsync<UbicacionRow>(
            "SELECT * FROM public.fn_af_ubicacion_listar(p_company_id => @c, p_search => @s)",
            new { c = CompanyId, s = codigo }));

        Assert.Equal(1L, fila.activos_registrados);
    }

    [SkippableFact]
    public async Task Ubicacion_valida_codigo_nombre_y_unicidad()
    {
        var codigo = "ZUB" + Sufijo();
        await CrearUbicacionAsync(codigo: codigo);

        Assert.Contains("Ya existe una ubicación",
            await ErrorDeAsync(() => CrearUbicacionAsync(codigo: codigo.ToLowerInvariant())));

        Assert.Contains("código de la ubicación es obligatorio",
            await ErrorDeAsync(() => CrearUbicacionAsync(codigo: " ")));

        Assert.Contains("nombre de la ubicación es obligatorio",
            await ErrorDeAsync(() => CrearUbicacionAsync(nombre: "  ")));
    }

    [SkippableFact]
    public async Task Ubicacion_con_padre_inexistente_es_rechazada()
    {
        Assert.Contains("ubicación contenedora seleccionada no existe",
            await ErrorDeAsync(() => CrearUbicacionAsync(padreId: -1)));
    }

    /// <summary>
    /// Los dos ciclos posibles. Sin estas guardas el CTE recursivo del listado se queda
    /// dando vueltas sobre el mismo par de filas.
    /// </summary>
    [SkippableFact]
    public async Task Ubicacion_no_admite_ciclos_ni_directos_ni_indirectos()
    {
        var padreId = await CrearUbicacionAsync(nombre: "EDIFICIO A");
        var hijoId = await CrearUbicacionAsync(nombre: "OFICINA 1", padreId: padreId);

        // Directo: la ubicación dentro de sí misma.
        Assert.Contains("no puede estar dentro de sí misma",
            await ErrorDeAsync(() => CrearUbicacionAsync(id: hijoId, nombre: "OFICINA 1", padreId: hijoId)));

        // Indirecto: el padre pasa a colgar de su propio hijo.
        Assert.Contains("ya depende de esta ubicación",
            await ErrorDeAsync(() => CrearUbicacionAsync(id: padreId, nombre: "EDIFICIO A", padreId: hijoId)));
    }

    [SkippableFact]
    public async Task Ubicacion_desactivada_desaparece_del_listado_de_activas()
    {
        var codigo = "ZUB" + Sufijo();
        var id = await CrearUbicacionAsync(codigo: codigo);

        await EjecutarAsync("CALL public.sp_af_ubicacion_desactivar(@c, @id, 'tester')",
            new { c = CompanyId, id });

        var activas = await ListarAsync<UbicacionRow>(
            "SELECT * FROM public.fn_af_ubicacion_listar(p_company_id => @c, p_solo_activos => TRUE, p_search => @s)",
            new { c = CompanyId, s = codigo });
        Assert.Empty(activas);

        var todas = await ListarAsync<UbicacionRow>(
            "SELECT * FROM public.fn_af_ubicacion_listar(p_company_id => @c, p_search => @s)",
            new { c = CompanyId, s = codigo });
        Assert.False(Assert.Single(todas).activo);

        Assert.Contains("No se encontró la ubicación",
            await ErrorDeAsync(() => EjecutarAsync(
                "CALL public.sp_af_ubicacion_desactivar(@c, @id, 'tester')",
                new { c = CompanyId, id = -1 })));
    }

    // ═══════════════════════════════════════════════ Formas de fila

    private sealed class MetodoRow
    {
        public short id { get; set; }
        public string nombre { get; set; } = string.Empty;
        public string? descripcion { get; set; }
        public bool implementado { get; set; }
    }

    private sealed class EstadoRow
    {
        public short id { get; set; }
        public string nombre { get; set; } = string.Empty;
        public string? descripcion { get; set; }
        public bool permite_depreciar { get; set; }
        public bool es_final { get; set; }
    }

    private sealed class TipoRow
    {
        public int id { get; set; }
        public string codigo { get; set; } = string.Empty;
        public string nombre { get; set; } = string.Empty;
        public string? descripcion { get; set; }
        public string? prefijo_codigo { get; set; }
        public decimal? vida_util_anios { get; set; }
        public short metodo_depreciacion_id { get; set; }
        public string? metodo_depreciacion { get; set; }
        public decimal porcentaje_residual { get; set; }
        public string? cuenta_activo { get; set; }
        public string? cuenta_depreciacion_acumulada { get; set; }
        public string? cuenta_gasto_depreciacion { get; set; }
        public string? cuenta_perdida_baja { get; set; }
        public bool activo { get; set; }
        public long activos_registrados { get; set; }
    }

    private sealed class UbicacionRow
    {
        public int id { get; set; }
        public string codigo { get; set; } = string.Empty;
        public string nombre { get; set; } = string.Empty;
        public int? padre_id { get; set; }
        public string? padre_nombre { get; set; }
        public string? ruta { get; set; }
        public string? direccion { get; set; }
        public string? responsable { get; set; }
        public bool activo { get; set; }
        public long activos_registrados { get; set; }
    }
}
