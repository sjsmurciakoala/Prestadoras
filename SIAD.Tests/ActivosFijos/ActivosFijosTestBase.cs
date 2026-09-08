using Dapper;
using Npgsql;
using SIAD.Tests.Infrastructure;

namespace SIAD.Tests.ActivosFijos;

/// <summary>
/// Base común de las pruebas del módulo ACTIVOS FIJOS (F1 — registro).
/// <para>
/// Ejercita directamente los procedimientos y funciones que publican
/// <c>Database/2026-09-08_af_activos_fijos_f1_registro.sql</c> y
/// <c>Database/2026-09-08_af_historial_depreciacion.sql</c>: ahí vive TODA la lógica de
/// negocio (herencia del tipo, derivados de línea recta, unicidad, apertura y cierre de la
/// asignación vigente), así que probar el SQL es probar el módulo. El servicio de C# solo
/// invoca y traduce el error.
/// </para>
/// <para>
/// Cada prueba corre dentro del <c>BEGIN … ROLLBACK</c> del harness, así que la base de
/// prueba queda intacta. Los códigos que se siembran llevan sufijo aleatorio para no chocar
/// con las 829 filas del histórico migrado de SIMAFI que ya viven en <c>af_activo_fijo</c>.
/// </para>
/// <para>
/// Sin la variable de entorno <c>SIAD_TEST_DB</c> las pruebas quedan <c>Skipped</c>
/// (lo resuelve <see cref="IntegrationTestBase.InitializeAsync"/>).
/// </para>
/// </summary>
public abstract class ActivosFijosTestBase : IntegrationTestBase
{
    // ── Catálogos de sistema: ids fijos que siembra el script del módulo ────────
    // Se referencian por constante y nunca por texto (regla de estados numéricos).

    /// <summary>Único método con <c>implementado = true</c>: el motor solo sabe línea recta.</summary>
    protected const short MetodoLineaRecta = 1;

    /// <summary>Declarado en el catálogo pero <c>implementado = false</c>: la BD lo rechaza.</summary>
    protected const short MetodoSaldoDecreciente = 2;

    protected const short EstadoEnUso = 1;              // permite_depreciar = true
    protected const short EstadoDisponible = 2;
    protected const short EstadoFueraDeServicio = 4;    // permite_depreciar = false, no final
    protected const short EstadoDadoDeBaja = 5;         // es_final = true  → descargado
    protected const short EstadoVendido = 6;            // es_final = true  → vendido

    /// <summary>
    /// Id que no existe, para probar los caminos de EDICIÓN contra un registro ausente.
    /// Tiene que ser POSITIVO y grande: los procedimientos tratan <c>p_id &lt;= 0</c> (y el
    /// nulo) como «registro nuevo», así que un -1 haría un alta en vez de fallar.
    /// </summary>
    protected const int IdInexistente = 1_999_999_999;

    protected ActivosFijosTestBase(PostgresFixture fixture) : base(fixture)
    {
    }

    // ══════════════════════════════════════════════════════════ Utilidades

    /// <summary>Fecha relativa a hoy: los procedimientos validan contra <c>current_date</c>.</summary>
    protected static DateOnly Hoy(int dias = 0) => DateOnly.FromDateTime(DateTime.Today).AddDays(dias);

    /// <summary>
    /// Dapper no sabe pasar <see cref="DateOnly"/> como parámetro, así que se manda
    /// <see cref="DateTime"/>. Npgsql lo envía entonces como <c>timestamp</c> y Postgres
    /// resuelve el procedimiento por tipo EXACTO: por eso cada parámetro de fecha del CALL
    /// lleva <c>::date</c>. Sin el cast falla con «no existe el procedimiento».
    /// </summary>
    protected static DateTime? AFecha(DateOnly? fecha) => fecha?.ToDateTime(TimeOnly.MinValue);

    /// <summary>Sufijo hexadecimal en mayúsculas: seguro dentro del regex del correlativo.</summary>
    protected static string Sufijo() => Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

    /// <summary>Fecha en el mismo formato en que se leen las columnas date (<c>::text</c>).</summary>
    protected static string Texto(DateOnly fecha)
        => fecha.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    protected Task<int> EjecutarAsync(string sql, object? parametros = null)
        => Connection.ExecuteAsync(new CommandDefinition(sql, parametros, Transaction));

    protected Task<T?> EscalarAsync<T>(string sql, object? parametros = null)
        => Connection.ExecuteScalarAsync<T>(new CommandDefinition(sql, parametros, Transaction));

    protected Task<T> UnicoAsync<T>(string sql, object? parametros = null)
        => Connection.QuerySingleAsync<T>(new CommandDefinition(sql, parametros, Transaction));

    /// <summary>Sin LINQ (regla del repo): la materialización va por el constructor de la lista.</summary>
    protected async Task<List<T>> ListarAsync<T>(string sql, object? parametros = null)
        => new List<T>(await Connection.QueryAsync<T>(new CommandDefinition(sql, parametros, Transaction)));

    /// <summary>
    /// Ejecuta algo que DEBE fallar y devuelve el texto del <c>RAISE EXCEPTION</c>.
    /// <para>
    /// Envuelve la llamada en un SAVEPOINT: un error de Postgres aborta la transacción del
    /// test y cualquier consulta posterior moriría con «current transaction is aborted».
    /// Con el savepoint la prueba puede encadenar varias validaciones y seguir leyendo.
    /// </para>
    /// </summary>
    protected async Task<string> ErrorDeAsync(Func<Task> accion)
    {
        var punto = "af_" + Sufijo().ToLowerInvariant();

        await EjecutarAsync($"SAVEPOINT {punto}");
        var error = await Record.ExceptionAsync(accion);
        await EjecutarAsync($"ROLLBACK TO SAVEPOINT {punto}");
        await EjecutarAsync($"RELEASE SAVEPOINT {punto}");

        Assert.NotNull(error);
        return error is PostgresException pg ? pg.MessageText : error!.Message;
    }

    // ══════════════════════════════════════════════════════════ Siembra: catálogos

    /// <summary>Tipo de activo nuevo (código y prefijo únicos). Devuelve el id que asigna el SP.</summary>
    protected async Task<int> CrearTipoAsync(
        string? codigo = null,
        string nombre = "TIPO DE PRUEBA",
        string? prefijoCodigo = null,
        decimal? vidaUtilAnios = null,
        short metodoDepreciacionId = MetodoLineaRecta,
        decimal porcentajeResidual = 0m,
        string? cuentaActivo = null,
        string? cuentaDepreciacionAcumulada = null,
        string? cuentaGastoDepreciacion = null,
        string? cuentaPerdidaBaja = null,
        bool activo = true,
        int? id = null,
        string usuario = "tester")
    {
        var salida = await UnicoAsync<SalidaId>(@"
            CALL public.sp_af_tipo_activo_guardar(
                @p_id, @p_company_id, @p_codigo, @p_nombre, @p_descripcion, @p_prefijo_codigo,
                @p_vida_util_anios::numeric, @p_metodo_depreciacion_id::smallint,
                @p_porcentaje_residual::numeric, @p_cuenta_activo, @p_cuenta_depreciacion_acumulada,
                @p_cuenta_gasto_depreciacion, @p_cuenta_perdida_baja, @p_activo, @p_usuario)",
            new
            {
                p_id = id,
                p_company_id = CompanyId,
                p_codigo = codigo ?? "ZTA" + Sufijo(),
                p_nombre = nombre,
                p_descripcion = (string?)"Tipo sembrado por las pruebas",
                p_prefijo_codigo = prefijoCodigo,
                p_vida_util_anios = vidaUtilAnios,
                p_metodo_depreciacion_id = metodoDepreciacionId,
                p_porcentaje_residual = porcentajeResidual,
                p_cuenta_activo = cuentaActivo,
                p_cuenta_depreciacion_acumulada = cuentaDepreciacionAcumulada,
                p_cuenta_gasto_depreciacion = cuentaGastoDepreciacion,
                p_cuenta_perdida_baja = cuentaPerdidaBaja,
                p_activo = activo,
                p_usuario = usuario
            });

        return salida.p_id;
    }

    /// <summary>Ubicación nueva (código único). <paramref name="padreId"/> arma la jerarquía.</summary>
    protected async Task<int> CrearUbicacionAsync(
        string? codigo = null,
        string nombre = "UBICACION DE PRUEBA",
        int? padreId = null,
        string? direccion = null,
        string? responsable = null,
        bool activo = true,
        int? id = null,
        string usuario = "tester")
    {
        var salida = await UnicoAsync<SalidaId>(@"
            CALL public.sp_af_ubicacion_guardar(
                @p_id, @p_company_id, @p_codigo, @p_nombre, @p_padre_id,
                @p_direccion, @p_responsable, @p_activo, @p_usuario)",
            new
            {
                p_id = id,
                p_company_id = CompanyId,
                p_codigo = codigo ?? "ZUB" + Sufijo(),
                p_nombre = nombre,
                p_padre_id = padreId,
                p_direccion = direccion,
                p_responsable = responsable,
                p_activo = activo,
                p_usuario = usuario
            });

        return salida.p_id;
    }

    /// <summary>Cargo del catálogo de Talento Humano (lo copia la asignación al activo).</summary>
    protected async Task<int> CrearCargoAsync(string? nombre = null)
        => await EscalarAsync<int>(@"
            INSERT INTO public.th_cargo (company_id, nombre, activo, usuariocreacion)
            VALUES (@c, @nombre, TRUE, 'tester')
            RETURNING id",
            new { c = CompanyId, nombre = nombre ?? "CARGO " + Sufijo() });

    /// <summary>Empleado del catálogo de Talento Humano (responsable del activo).</summary>
    protected async Task<int> CrearEmpleadoAsync(string? nombre = null, int? cargoId = null)
        => await EscalarAsync<int>(@"
            INSERT INTO public.th_empleado (company_id, codigo, nombre, cargo_id, activo, usuariocreacion)
            VALUES (@c, @codigo, @nombre, @cargo, TRUE, 'tester')
            RETURNING id",
            new
            {
                c = CompanyId,
                codigo = "ZE" + Sufijo(),
                nombre = nombre ?? "EMPLEADO " + Sufijo(),
                cargo = cargoId
            });

    // ══════════════════════════════════════════════════════════ Siembra: activo

    /// <summary>
    /// Invoca <c>sp_af_activo_guardar</c> con la misma forma de llamada que el servicio.
    /// Devuelve el id y el código (los dos parámetros INOUT del procedimiento).
    /// </summary>
    protected async Task<(int Id, string Codigo)> GuardarActivoAsync(
        int? id = null,
        string? codigo = null,
        string? descripcion = "ACTIVO DE PRUEBA",
        int? tipoActivoId = null,
        short? estadoActivoId = EstadoEnUso,
        int? ubicacionId = null,
        int? empleadoId = null,
        string? responsable = null,
        string? cargoResponsable = null,
        string? codProveedor = null,
        long? centroCostoId = null,
        string? marca = null,
        string? modelo = null,
        string? serie = null,
        string? placa = null,
        string? codigoBarra = null,
        DateOnly? fechaCompra = null,
        bool fechaCompraNula = false,
        DateOnly? fechaInicioDepreciacion = null,
        decimal valorCompra = 10_000m,
        decimal? valorRescate = null,
        decimal? vidaUtilAnios = null,
        short? metodoDepreciacionId = null,
        bool depreciar = false,
        decimal depreciacionAcumulada = 0m,
        string? cuentaContable = null,
        string? cuentaDepreciacion = null,
        string? cuentaGasto = null,
        string usuario = "tester")
    {
        // El tipo tiene que ser explícito: sin él, el compilador no sabe reconciliar el
        // null literal de la rama izquierda con el DateOnly de la derecha.
        DateOnly? compra = fechaCompraNula ? null : fechaCompra ?? Hoy(-30);

        var salida = await UnicoAsync<SalidaActivo>(@"
            CALL public.sp_af_activo_guardar(
                @p_id, @p_codigo_activo, @p_company_id, @p_descripcion, @p_clase,
                @p_tipo_activo_id, @p_estado_activo_id::smallint, @p_ubicacion_id, @p_empleado_id,
                @p_responsable, @p_cargo_responsable, @p_cod_proveedor, @p_centro_costo_id,
                @p_marca, @p_modelo, @p_serie, @p_placa, @p_codigo_barra, @p_numero_factura,
                @p_fecha_compra::date, @p_fecha_inicio_depreciacion::date,
                @p_valor_compra::numeric, @p_valor_rescate::numeric,
                @p_vida_util_anios::numeric, @p_metodo_depreciacion_id::smallint,
                @p_depreciar, @p_depreciacion_acumulada::numeric,
                @p_cuenta_contable, @p_cuenta_depreciacion, @p_cuenta_gasto,
                @p_poliza_seguro, @p_poliza_vence::date, @p_garantia_vence::date,
                @p_propiedades_especiales, @p_observacion, @p_usuario)",
            new
            {
                p_id = id,
                p_codigo_activo = codigo,
                p_company_id = CompanyId,
                p_descripcion = descripcion,
                p_clase = (string?)null,
                p_tipo_activo_id = tipoActivoId,
                p_estado_activo_id = estadoActivoId,
                p_ubicacion_id = ubicacionId,
                p_empleado_id = empleadoId,
                p_responsable = responsable,
                p_cargo_responsable = cargoResponsable,
                p_cod_proveedor = codProveedor,
                p_centro_costo_id = centroCostoId,
                p_marca = marca,
                p_modelo = modelo,
                p_serie = serie,
                p_placa = placa,
                p_codigo_barra = codigoBarra,
                p_numero_factura = (string?)null,
                p_fecha_compra = AFecha(compra),
                p_fecha_inicio_depreciacion = AFecha(fechaInicioDepreciacion),
                p_valor_compra = valorCompra,
                p_valor_rescate = valorRescate,
                p_vida_util_anios = vidaUtilAnios,
                p_metodo_depreciacion_id = metodoDepreciacionId,
                p_depreciar = depreciar,
                p_depreciacion_acumulada = depreciacionAcumulada,
                p_cuenta_contable = cuentaContable,
                p_cuenta_depreciacion = cuentaDepreciacion,
                p_cuenta_gasto = cuentaGasto,
                p_poliza_seguro = (string?)null,
                p_poliza_vence = (DateTime?)null,
                p_garantia_vence = (DateTime?)null,
                p_propiedades_especiales = (string?)null,
                p_observacion = (string?)null,
                p_usuario = usuario
            });

        return (salida.p_id, salida.p_codigo_activo ?? string.Empty);
    }

    /// <summary>Reasignación explícita del activo (<c>sp_af_activo_asignar</c>).</summary>
    protected Task AsignarAsync(
        int activoFijoId,
        DateOnly? fecha = null,
        int? empleadoId = null,
        int? ubicacionId = null,
        long? centroCostoId = null,
        string? motivo = null,
        string usuario = "tester")
        => EjecutarAsync(@"
            CALL public.sp_af_activo_asignar(@p_company_id, @p_activo_fijo_id, @p_fecha::date,
                                             @p_empleado_id, @p_ubicacion_id, @p_centro_costo_id,
                                             @p_motivo, @p_usuario)",
            new
            {
                p_company_id = CompanyId,
                p_activo_fijo_id = activoFijoId,
                p_fecha = AFecha(fecha),
                p_empleado_id = empleadoId,
                p_ubicacion_id = ubicacionId,
                p_centro_costo_id = centroCostoId,
                p_motivo = motivo,
                p_usuario = usuario
            });

    /// <summary>Alta o edición de un componente (<c>sp_af_activo_componente_guardar</c>).</summary>
    protected async Task<int> GuardarComponenteAsync(
        int activoFijoId,
        string? descripcion = "COMPONENTE DE PRUEBA",
        int? id = null,
        string? marca = null,
        string? modelo = null,
        string? serie = null,
        decimal? cantidad = 1m,
        decimal? valor = 0m,
        string? observacion = null,
        string usuario = "tester")
    {
        var salida = await UnicoAsync<SalidaId>(@"
            CALL public.sp_af_activo_componente_guardar(
                @p_id, @p_company_id, @p_activo_fijo_id, @p_descripcion, @p_marca, @p_modelo,
                @p_serie, @p_cantidad::numeric, @p_valor::numeric, @p_observacion, @p_usuario)",
            new
            {
                p_id = id,
                p_company_id = CompanyId,
                p_activo_fijo_id = activoFijoId,
                p_descripcion = descripcion,
                p_marca = marca,
                p_modelo = modelo,
                p_serie = serie,
                p_cantidad = cantidad,
                p_valor = valor,
                p_observacion = observacion,
                p_usuario = usuario
            });

        return salida.p_id;
    }

    protected Task EliminarComponenteAsync(int componenteId)
        => EjecutarAsync("CALL public.sp_af_activo_componente_eliminar(@c, @id)",
            new { c = CompanyId, id = componenteId });

    /// <summary>Fila del detalle mensual migrado de SIMAFI (<c>af_activo_fijo_depreciacion</c>).</summary>
    protected Task SembrarDepreciacionAsync(
        int? activoFijoId,
        string? codigoActivo,
        short anio,
        short mes,
        decimal valorDepreciado,
        decimal valorNetoLibros = 0m,
        long? companyId = null)
        => EjecutarAsync(@"
            INSERT INTO public.af_activo_fijo_depreciacion
                   (company_id, activo_fijo_id, codigo_activo, anio, mes, fecha_depreciacion,
                    valor_depreciado, valor_neto_libros, descripcion)
            VALUES (@c, @activo, @codigo, @anio, @mes,
                    CASE WHEN @anio > 0 THEN make_date(@anio::int, @mes::int, 1) END,
                    @valor, @neto, 'detalle sembrado por las pruebas')",
            new
            {
                c = companyId ?? CompanyId,
                activo = activoFijoId,
                codigo = codigoActivo,
                anio,
                mes,
                valor = valorDepreciado,
                neto = valorNetoLibros
            });

    // ══════════════════════════════════════════════════════════ Lecturas

    /// <summary>
    /// Fila cruda del maestro. Las fechas se leen como texto (<c>::text</c>) para comparar
    /// contra literales «AAAA-MM-DD» sin depender del mapeo date/DateOnly del driver.
    /// </summary>
    protected Task<ActivoRow> LeerActivoAsync(int id) => UnicoAsync<ActivoRow>(@"
        SELECT id, codigo_activo, descripcion, tipo_activo_id, estado_activo_id, ubicacion_id,
               empleado_id, responsable, cargo_responsable, codigo_barra, cod_proveedor,
               centro_costo_id, vida_util_anios, vida_util_periodos, metodo_depreciacion_id,
               depreciar, valor_compra, valor_rescate, valor_a_depreciar, depreciacion_acumulada,
               valor_depreciado, depreciacion_mensual, depreciacion_diaria, valor_libros,
               cuenta_contable, cuenta_depreciacion, cuenta_gasto, descargado, vendido,
               fecha_compra::text              AS fecha_compra,
               fecha_inicio_depreciacion::text AS fecha_inicio_depreciacion,
               fecha_fin_depreciacion::text    AS fecha_fin_depreciacion,
               fecha_asignacion::text          AS fecha_asignacion
          FROM public.af_activo_fijo
         WHERE company_id = @c AND id = @id",
        new { c = CompanyId, id });

    protected Task<List<AsignacionRow>> LeerAsignacionesAsync(int activoFijoId)
        => ListarAsync<AsignacionRow>(@"
            SELECT id, fecha_desde::text AS fecha_desde, fecha_hasta::text AS fecha_hasta,
                   vigente, empleado_id, responsable, cargo_responsable, ubicacion_id,
                   ubicacion, centro_costo, motivo, usuariocreacion
              FROM public.fn_af_activo_asignacion_listar(@c, @activo)",
            new { c = CompanyId, activo = activoFijoId });

    protected async Task<int> ContarAsignacionesVigentesAsync(int activoFijoId)
        => await EscalarAsync<int>(@"
            SELECT count(*)::int FROM public.af_activo_asignacion
             WHERE company_id = @c AND activo_fijo_id = @activo AND fecha_hasta IS NULL",
            new { c = CompanyId, activo = activoFijoId });

    protected Task<List<ComponenteRow>> LeerComponentesAsync(int activoFijoId)
        => ListarAsync<ComponenteRow>(
            "SELECT * FROM public.fn_af_activo_componente_listar(@c, @activo)",
            new { c = CompanyId, activo = activoFijoId });

    protected Task<ResumenRow> LeerResumenAsync()
        => UnicoAsync<ResumenRow>("SELECT * FROM public.fn_af_activo_resumen(@c)", new { c = CompanyId });

    // ══════════════════════════════════════════════════════════ Formas de fila

    /// <summary>Parámetro INOUT <c>p_id</c> que devuelve el CALL de los guardados.</summary>
    protected sealed class SalidaId
    {
        public int p_id { get; set; }
    }

    /// <summary>Los dos INOUT de <c>sp_af_activo_guardar</c>.</summary>
    protected sealed class SalidaActivo
    {
        public int p_id { get; set; }
        public string? p_codigo_activo { get; set; }
    }

    protected sealed class ActivoRow
    {
        public int id { get; set; }
        public string codigo_activo { get; set; } = string.Empty;
        public string descripcion { get; set; } = string.Empty;
        public int? tipo_activo_id { get; set; }
        public short? estado_activo_id { get; set; }
        public int? ubicacion_id { get; set; }
        public int? empleado_id { get; set; }
        public string? responsable { get; set; }
        public string? cargo_responsable { get; set; }
        public string? codigo_barra { get; set; }
        public string? cod_proveedor { get; set; }
        public long? centro_costo_id { get; set; }
        public decimal? vida_util_anios { get; set; }
        public decimal? vida_util_periodos { get; set; }
        public short? metodo_depreciacion_id { get; set; }
        public bool depreciar { get; set; }
        public decimal valor_compra { get; set; }
        public decimal valor_rescate { get; set; }
        public decimal valor_a_depreciar { get; set; }
        public decimal depreciacion_acumulada { get; set; }
        public decimal valor_depreciado { get; set; }
        public decimal depreciacion_mensual { get; set; }
        public decimal depreciacion_diaria { get; set; }
        public decimal valor_libros { get; set; }
        public string? cuenta_contable { get; set; }
        public string? cuenta_depreciacion { get; set; }
        public string? cuenta_gasto { get; set; }
        public bool descargado { get; set; }
        public bool vendido { get; set; }
        public string? fecha_compra { get; set; }
        public string? fecha_inicio_depreciacion { get; set; }
        public string? fecha_fin_depreciacion { get; set; }
        public string? fecha_asignacion { get; set; }
    }

    protected sealed class AsignacionRow
    {
        public int id { get; set; }
        public string fecha_desde { get; set; } = string.Empty;
        public string? fecha_hasta { get; set; }
        public bool vigente { get; set; }
        public int? empleado_id { get; set; }
        public string? responsable { get; set; }
        public string? cargo_responsable { get; set; }
        public int? ubicacion_id { get; set; }
        public string? ubicacion { get; set; }
        public string? centro_costo { get; set; }
        public string? motivo { get; set; }
        public string? usuariocreacion { get; set; }
    }

    protected sealed class ComponenteRow
    {
        public int id { get; set; }
        public string descripcion { get; set; } = string.Empty;
        public string? marca { get; set; }
        public string? modelo { get; set; }
        public string? serie { get; set; }
        public decimal cantidad { get; set; }
        public decimal valor { get; set; }
        public string? observacion { get; set; }
    }

    protected sealed class ResumenRow
    {
        public long total_activos { get; set; }
        public long en_patrimonio { get; set; }
        public long pendientes_completar { get; set; }
        public long sin_responsable { get; set; }
        public decimal valor_compra { get; set; }
        public decimal depreciacion_acumulada { get; set; }
        public decimal valor_libros { get; set; }
    }
}
