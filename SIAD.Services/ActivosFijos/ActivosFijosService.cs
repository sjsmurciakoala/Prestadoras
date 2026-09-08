using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.ActivosFijos;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.ActivosFijos;

/// <summary>
/// Maestro de activos fijos. Las validaciones de negocio (herencia del tipo, coherencia de
/// valores, unicidad de código, apertura y cierre de la asignación vigente) viven en el
/// procedimiento <c>sp_af_activo_guardar</c>; este servicio invoca y traduce el error de
/// Postgres a una excepción que el controlador convierte en 400.
/// </summary>
public sealed class ActivosFijosService : IActivosFijosService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public ActivosFijosService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    // ── Listado y ficha ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ActivoFijoListItemDto>> GetAsync(ActivoFijoFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ActivoFijoFilterDto();
        var connection = await AbrirConexionAsync(ct);

        var filas = await connection.QueryAsync<ActivoFijoListItemDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo_activo AS CodigoActivo, descripcion AS Descripcion,
                   tipo_activo_id AS TipoActivoId, tipo_activo AS TipoActivo,
                   estado_activo_id AS EstadoActivoId, estado_activo AS EstadoActivo,
                   estado_es_final AS EstadoEsFinal,
                   ubicacion_id AS UbicacionId, ubicacion AS Ubicacion,
                   empleado_id AS EmpleadoId, responsable AS Responsable,
                   marca AS Marca, modelo AS Modelo, serie AS Serie, placa AS Placa,
                   fecha_compra AS FechaCompra, valor_compra AS ValorCompra,
                   valor_rescate AS ValorRescate, depreciacion_acumulada AS DepreciacionAcumulada,
                   valor_libros AS ValorLibros, depreciar AS Depreciar,
                   pendiente_completar AS PendienteCompletar
              FROM public.fn_af_activo_listar(@CompanyId, @Search, @TipoId, @EstadoId,
                                              @UbicacionId, @EmpleadoId, @Pendientes)",
            new
            {
                CompanyId = EnsureCompanyId(),
                Search = Patron(filtro.Search),
                TipoId = filtro.TipoActivoId,
                EstadoId = filtro.EstadoActivoId,
                UbicacionId = filtro.UbicacionId,
                EmpleadoId = filtro.EmpleadoId,
                Pendientes = filtro.SoloPendientes
            }, TransaccionActual(), cancellationToken: ct));

        return new List<ActivoFijoListItemDto>(filas);
    }

    public async Task<ActivoFijoResumenDto> GetResumenAsync(CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);

        var resumen = await connection.QuerySingleOrDefaultAsync<ActivoFijoResumenDto>(new CommandDefinition(@"
            SELECT total_activos AS TotalActivos, en_patrimonio AS EnPatrimonio,
                   pendientes_completar AS PendientesCompletar, sin_responsable AS SinResponsable,
                   valor_compra AS ValorCompra, depreciacion_acumulada AS DepreciacionAcumulada,
                   valor_libros AS ValorLibros
              FROM public.fn_af_activo_resumen(@CompanyId)",
            new { CompanyId = EnsureCompanyId() }, TransaccionActual(), cancellationToken: ct));

        return resumen ?? new ActivoFijoResumenDto();
    }

    public async Task<ActivoFijoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var connection = await AbrirConexionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<ActivoFijoEditDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo_activo AS CodigoActivo, descripcion AS Descripcion, clase AS Clase,
                   tipo_activo_id AS TipoActivoId, estado_activo_id AS EstadoActivoId,
                   ubicacion_id AS UbicacionId, empleado_id AS EmpleadoId,
                   responsable AS Responsable, cargo_responsable AS CargoResponsable,
                   cod_proveedor AS CodProveedor, centro_costo_id AS CentroCostoId,
                   marca AS Marca, modelo AS Modelo, serie AS Serie, placa AS Placa,
                   codigo_barra AS CodigoBarra, numero_factura AS NumeroFactura,
                   fecha_compra AS FechaCompra, fecha_inicio_depreciacion AS FechaInicioDepreciacion,
                   fecha_fin_depreciacion AS FechaFinDepreciacion,
                   valor_compra AS ValorCompra, valor_rescate AS ValorRescate,
                   vida_util_anios AS VidaUtilAnios, metodo_depreciacion_id AS MetodoDepreciacionId,
                   depreciar AS Depreciar, depreciacion_acumulada AS DepreciacionAcumulada,
                   depreciacion_mensual AS DepreciacionMensual, depreciacion_diaria AS DepreciacionDiaria,
                   valor_libros AS ValorLibros, cuenta_contable AS CuentaContable,
                   cuenta_depreciacion AS CuentaDepreciacion, cuenta_gasto AS CuentaGasto,
                   poliza_seguro AS PolizaSeguro, poliza_vence AS PolizaVence,
                   garantia_vence AS GarantiaVence, propiedades_especiales AS PropiedadesEspeciales,
                   observacion AS Observacion, tipo_activo AS TipoActivo, estado_activo AS EstadoActivo,
                   ubicacion AS Ubicacion, proveedor_nombre AS ProveedorNombre, centro_costo AS CentroCosto
              FROM public.fn_af_activo_obtener(@CompanyId, @Id)",
            new { CompanyId = EnsureCompanyId(), Id = id }, TransaccionActual(), cancellationToken: ct));
    }

    public async Task<ActivoFijoEditDto> GuardarAsync(ActivoFijoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var connection = await AbrirConexionAsync(ct);

        var salida = await connection.QuerySingleAsync<SalidaGuardarActivo>(new CommandDefinition(@"
            CALL public.sp_af_activo_guardar(
                @p_id, @p_codigo_activo, @p_company_id, @p_descripcion, @p_clase,
                @p_tipo_activo_id, @p_estado_activo_id, @p_ubicacion_id, @p_empleado_id,
                @p_responsable, @p_cargo_responsable, @p_cod_proveedor, @p_centro_costo_id,
                @p_marca, @p_modelo, @p_serie, @p_placa, @p_codigo_barra, @p_numero_factura,
                @p_fecha_compra::date, @p_fecha_inicio_depreciacion::date,
                @p_valor_compra, @p_valor_rescate,
                @p_vida_util_anios, @p_metodo_depreciacion_id, @p_depreciar, @p_depreciacion_acumulada,
                @p_cuenta_contable, @p_cuenta_depreciacion, @p_cuenta_gasto,
                @p_poliza_seguro, @p_poliza_vence::date, @p_garantia_vence::date,
                @p_propiedades_especiales, @p_observacion, @p_usuario)",
            new
            {
                p_id = dto.Id,
                p_codigo_activo = dto.CodigoActivo,
                p_company_id = EnsureCompanyId(),
                p_descripcion = dto.Descripcion,
                p_clase = dto.Clase,
                p_tipo_activo_id = dto.TipoActivoId,
                p_estado_activo_id = dto.EstadoActivoId,
                p_ubicacion_id = dto.UbicacionId,
                p_empleado_id = dto.EmpleadoId,
                p_responsable = dto.Responsable,
                p_cargo_responsable = dto.CargoResponsable,
                p_cod_proveedor = dto.CodProveedor,
                p_centro_costo_id = dto.CentroCostoId,
                p_marca = dto.Marca,
                p_modelo = dto.Modelo,
                p_serie = dto.Serie,
                p_placa = dto.Placa,
                p_codigo_barra = dto.CodigoBarra,
                p_numero_factura = dto.NumeroFactura,
                p_fecha_compra = AFecha(dto.FechaCompra),
                p_fecha_inicio_depreciacion = AFecha(dto.FechaInicioDepreciacion),
                p_valor_compra = dto.ValorCompra,
                p_valor_rescate = dto.ValorRescate,
                p_vida_util_anios = dto.VidaUtilAnios,
                p_metodo_depreciacion_id = dto.MetodoDepreciacionId,
                p_depreciar = dto.Depreciar,
                p_depreciacion_acumulada = dto.DepreciacionAcumulada,
                p_cuenta_contable = dto.CuentaContable,
                p_cuenta_depreciacion = dto.CuentaDepreciacion,
                p_cuenta_gasto = dto.CuentaGasto,
                p_poliza_seguro = dto.PolizaSeguro,
                p_poliza_vence = AFecha(dto.PolizaVence),
                p_garantia_vence = AFecha(dto.GarantiaVence),
                p_propiedades_especiales = dto.PropiedadesEspeciales,
                p_observacion = dto.Observacion,
                p_usuario = Usuario(user)
            }, TransaccionActual(), cancellationToken: ct));

        // Se relee para devolver los derivados que calculó la base (cuota mensual y
        // diaria, valor en libros, fin de vida útil y las cuentas heredadas del tipo).
        var guardado = await GetByIdAsync(salida.p_id, ct);
        return guardado ?? dto;
    }

    // ── Asignaciones ────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ActivoAsignacionDto>> GetAsignacionesAsync(int activoId, CancellationToken ct = default)
    {
        if (activoId <= 0) return Array.Empty<ActivoAsignacionDto>();
        var connection = await AbrirConexionAsync(ct);

        var filas = await connection.QueryAsync<ActivoAsignacionDto>(new CommandDefinition(@"
            SELECT id AS Id, fecha_desde AS FechaDesde, fecha_hasta AS FechaHasta, vigente AS Vigente,
                   empleado_id AS EmpleadoId, responsable AS Responsable,
                   cargo_responsable AS CargoResponsable, ubicacion_id AS UbicacionId,
                   ubicacion AS Ubicacion, centro_costo AS CentroCosto, motivo AS Motivo,
                   usuariocreacion AS UsuarioCreacion
              FROM public.fn_af_activo_asignacion_listar(@CompanyId, @ActivoId)",
            new { CompanyId = EnsureCompanyId(), ActivoId = activoId }, TransaccionActual(), cancellationToken: ct));

        return new List<ActivoAsignacionDto>(filas);
    }

    public async Task AsignarAsync(int activoId, ActivoAsignacionRequestDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var connection = await AbrirConexionAsync(ct);

        await connection.ExecuteAsync(new CommandDefinition(@"
            CALL public.sp_af_activo_asignar(@p_company_id, @p_activo_fijo_id, @p_fecha::date,
                                             @p_empleado_id, @p_ubicacion_id, @p_centro_costo_id,
                                             @p_motivo, @p_usuario)",
            new
            {
                p_company_id = EnsureCompanyId(),
                p_activo_fijo_id = activoId,
                p_fecha = AFecha(dto.Fecha),
                p_empleado_id = dto.EmpleadoId,
                p_ubicacion_id = dto.UbicacionId,
                p_centro_costo_id = dto.CentroCostoId,
                p_motivo = dto.Motivo,
                p_usuario = Usuario(user)
            }, TransaccionActual(), cancellationToken: ct));
    }

    // ── Componentes ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<ActivoComponenteDto>> GetComponentesAsync(int activoId, CancellationToken ct = default)
    {
        if (activoId <= 0) return Array.Empty<ActivoComponenteDto>();
        var connection = await AbrirConexionAsync(ct);

        var filas = await connection.QueryAsync<ActivoComponenteDto>(new CommandDefinition(@"
            SELECT id AS Id, descripcion AS Descripcion, marca AS Marca, modelo AS Modelo,
                   serie AS Serie, cantidad AS Cantidad, valor AS Valor, observacion AS Observacion
              FROM public.fn_af_activo_componente_listar(@CompanyId, @ActivoId)",
            new { CompanyId = EnsureCompanyId(), ActivoId = activoId }, TransaccionActual(), cancellationToken: ct));

        return new List<ActivoComponenteDto>(filas);
    }

    public async Task<ActivoComponenteDto> GuardarComponenteAsync(int activoId, ActivoComponenteDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var connection = await AbrirConexionAsync(ct);

        var id = await connection.QuerySingleAsync<int?>(new CommandDefinition(@"
            CALL public.sp_af_activo_componente_guardar(@p_id, @p_company_id, @p_activo_fijo_id,
                                                        @p_descripcion, @p_marca, @p_modelo, @p_serie,
                                                        @p_cantidad, @p_valor, @p_observacion, @p_usuario)",
            new
            {
                p_id = dto.Id,
                p_company_id = EnsureCompanyId(),
                p_activo_fijo_id = activoId,
                p_descripcion = dto.Descripcion,
                p_marca = dto.Marca,
                p_modelo = dto.Modelo,
                p_serie = dto.Serie,
                p_cantidad = dto.Cantidad,
                p_valor = dto.Valor,
                p_observacion = dto.Observacion,
                p_usuario = Usuario(user)
            }, TransaccionActual(), cancellationToken: ct));

        dto.Id = id;
        return dto;
    }

    public async Task EliminarComponenteAsync(int componenteId, CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "CALL public.sp_af_activo_componente_eliminar(@p_company_id, @p_id)",
            new { p_company_id = EnsureCompanyId(), p_id = componenteId },
            TransaccionActual(), cancellationToken: ct));
    }

    // ── Infraestructura ─────────────────────────────────────────────────────

    /// <summary>Fila que devuelve <c>CALL sp_af_activo_guardar</c> por sus parámetros INOUT.</summary>
    private sealed class SalidaGuardarActivo
    {
        public int p_id { get; set; }
        public string? p_codigo_activo { get; set; }
    }

    private long EnsureCompanyId()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        return companyId;
    }

    private async Task<DbConnection> AbrirConexionAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }

    private DbTransaction? TransaccionActual() => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string Usuario(string? usuario) => string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario.Trim();

    private static string? Patron(string? search)
        => string.IsNullOrWhiteSpace(search) ? null : $"%{search.Trim()}%";

    /// <summary>
    /// Dapper no sabe pasar <see cref="DateOnly"/> como parámetro, así que se envía como
    /// <see cref="DateTime"/>. Ojo: Npgsql lo manda entonces como <c>timestamp</c>, y Postgres
    /// resuelve la sobrecarga del procedimiento por tipo EXACTO sin castear a <c>date</c>. Por eso
    /// cada parámetro de fecha lleva <c>::date</c> en el CALL; sin él falla con
    /// "no existe el procedimiento".
    /// </summary>
    private static DateTime? AFecha(DateOnly? fecha)
        => fecha.HasValue ? fecha.Value.ToDateTime(TimeOnly.MinValue) : null;
}
