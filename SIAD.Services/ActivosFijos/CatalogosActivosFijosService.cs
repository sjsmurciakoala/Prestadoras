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
/// Catálogos de Activos Fijos. Toda la lógica de consulta y de escritura vive en la base
/// de datos (funciones <c>fn_af_*</c> y procedimientos <c>sp_af_*</c> del script
/// 2026-09-08_af_activos_fijos_f1_registro.sql); aquí solo se invocan.
/// <para>
/// <see cref="ICurrentCompanyService"/> resuelve la empresa porque Dapper no pasa por el
/// filtro global multiempresa de <see cref="SiadDbContext"/>.
/// </para>
/// </summary>
public sealed class CatalogosActivosFijosService : ICatalogosActivosFijosService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public CatalogosActivosFijosService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    // ── Catálogos de sistema ────────────────────────────────────────────────

    public async Task<IReadOnlyList<CatalogoAfDto>> GetMetodosDepreciacionAsync(CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        var filas = await connection.QueryAsync<CatalogoAfDto>(new CommandDefinition(
            "SELECT id AS Id, nombre AS Nombre, descripcion AS Descripcion, implementado AS Implementado " +
            "FROM public.fn_af_metodo_depreciacion_listar()",
            transaction: TransaccionActual(), cancellationToken: ct));

        return new List<CatalogoAfDto>(filas);
    }

    public async Task<IReadOnlyList<CatalogoAfDto>> GetEstadosAsync(CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        var filas = await connection.QueryAsync<CatalogoAfDto>(new CommandDefinition(
            "SELECT id AS Id, nombre AS Nombre, descripcion AS Descripcion, " +
            "       permite_depreciar AS PermiteDepreciar, es_final AS EsFinal " +
            "FROM public.fn_af_estado_activo_listar()",
            transaction: TransaccionActual(), cancellationToken: ct));

        return new List<CatalogoAfDto>(filas);
    }

    // ── Tipos de activo ─────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TipoActivoListItemDto>> GetTiposAsync(bool? soloActivos, string? search, CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        var filas = await connection.QueryAsync<TipoActivoListItemDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   prefijo_codigo AS PrefijoCodigo, vida_util_anios AS VidaUtilAnios,
                   metodo_depreciacion_id AS MetodoDepreciacionId, metodo_depreciacion AS MetodoDepreciacion,
                   porcentaje_residual AS PorcentajeResidual, cuenta_activo AS CuentaActivo,
                   cuenta_depreciacion_acumulada AS CuentaDepreciacionAcumulada,
                   cuenta_gasto_depreciacion AS CuentaGastoDepreciacion,
                   cuenta_perdida_baja AS CuentaPerdidaBaja, activo AS Activo,
                   activos_registrados AS ActivosRegistrados
              FROM public.fn_af_tipo_activo_listar(@CompanyId, @SoloActivos, @Search)",
            new { CompanyId = EnsureCompanyId(), SoloActivos = soloActivos, Search = Patron(search) },
            TransaccionActual(), cancellationToken: ct));

        return new List<TipoActivoListItemDto>(filas);
    }

    public async Task<IReadOnlyList<TipoActivoLookupDto>> GetTiposLookupAsync(CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        var filas = await connection.QueryAsync<TipoActivoLookupDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, vida_util_anios AS VidaUtilAnios,
                   metodo_depreciacion_id AS MetodoDepreciacionId, porcentaje_residual AS PorcentajeResidual,
                   cuenta_activo AS CuentaActivo,
                   cuenta_depreciacion_acumulada AS CuentaDepreciacionAcumulada,
                   cuenta_gasto_depreciacion AS CuentaGastoDepreciacion
              FROM public.fn_af_tipo_activo_listar(@CompanyId, TRUE, NULL)",
            new { CompanyId = EnsureCompanyId() }, TransaccionActual(), cancellationToken: ct));

        return new List<TipoActivoLookupDto>(filas);
    }

    public async Task<TipoActivoEditDto?> GetTipoByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var connection = await AbrirConexionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<TipoActivoEditDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   prefijo_codigo AS PrefijoCodigo, vida_util_anios AS VidaUtilAnios,
                   metodo_depreciacion_id AS MetodoDepreciacionId, porcentaje_residual AS PorcentajeResidual,
                   cuenta_activo AS CuentaActivo,
                   cuenta_depreciacion_acumulada AS CuentaDepreciacionAcumulada,
                   cuenta_gasto_depreciacion AS CuentaGastoDepreciacion,
                   cuenta_perdida_baja AS CuentaPerdidaBaja, activo AS Activo
              FROM public.fn_af_tipo_activo_obtener(@CompanyId, @Id)",
            new { CompanyId = EnsureCompanyId(), Id = id }, TransaccionActual(), cancellationToken: ct));
    }

    public async Task<TipoActivoEditDto> GuardarTipoAsync(TipoActivoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var connection = await AbrirConexionAsync(ct);

        var id = await connection.QuerySingleAsync<int?>(new CommandDefinition(@"
            CALL public.sp_af_tipo_activo_guardar(
                @p_id, @p_company_id, @p_codigo, @p_nombre, @p_descripcion, @p_prefijo_codigo,
                @p_vida_util_anios, @p_metodo_depreciacion_id, @p_porcentaje_residual,
                @p_cuenta_activo, @p_cuenta_depreciacion_acumulada, @p_cuenta_gasto_depreciacion,
                @p_cuenta_perdida_baja, @p_activo, @p_usuario)",
            new
            {
                p_id = dto.Id,
                p_company_id = EnsureCompanyId(),
                p_codigo = dto.Codigo,
                p_nombre = dto.Nombre,
                p_descripcion = dto.Descripcion,
                p_prefijo_codigo = dto.PrefijoCodigo,
                p_vida_util_anios = dto.VidaUtilAnios,
                p_metodo_depreciacion_id = dto.MetodoDepreciacionId,
                p_porcentaje_residual = dto.PorcentajeResidual,
                p_cuenta_activo = dto.CuentaActivo,
                p_cuenta_depreciacion_acumulada = dto.CuentaDepreciacionAcumulada,
                p_cuenta_gasto_depreciacion = dto.CuentaGastoDepreciacion,
                p_cuenta_perdida_baja = dto.CuentaPerdidaBaja,
                p_activo = dto.Activo,
                p_usuario = Usuario(user)
            }, TransaccionActual(), cancellationToken: ct));

        dto.Id = id;
        return dto;
    }

    public async Task DesactivarTipoAsync(int id, string user, CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "CALL public.sp_af_tipo_activo_desactivar(@p_company_id, @p_id, @p_usuario)",
            new { p_company_id = EnsureCompanyId(), p_id = id, p_usuario = Usuario(user) },
            TransaccionActual(), cancellationToken: ct));
    }

    // ── Ubicaciones ─────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<UbicacionActivoListItemDto>> GetUbicacionesAsync(bool? soloActivos, string? search, CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        var filas = await connection.QueryAsync<UbicacionActivoListItemDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, padre_id AS PadreId,
                   padre_nombre AS PadreNombre, ruta AS Ruta, direccion AS Direccion,
                   responsable AS Responsable, activo AS Activo,
                   activos_registrados AS ActivosRegistrados
              FROM public.fn_af_ubicacion_listar(@CompanyId, @SoloActivos, @Search)",
            new { CompanyId = EnsureCompanyId(), SoloActivos = soloActivos, Search = Patron(search) },
            TransaccionActual(), cancellationToken: ct));

        return new List<UbicacionActivoListItemDto>(filas);
    }

    public async Task<IReadOnlyList<UbicacionActivoLookupDto>> GetUbicacionesLookupAsync(CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        var filas = await connection.QueryAsync<UbicacionActivoLookupDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, ruta AS Ruta
              FROM public.fn_af_ubicacion_listar(@CompanyId, TRUE, NULL)",
            new { CompanyId = EnsureCompanyId() }, TransaccionActual(), cancellationToken: ct));

        return new List<UbicacionActivoLookupDto>(filas);
    }

    public async Task<UbicacionActivoEditDto?> GetUbicacionByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var connection = await AbrirConexionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<UbicacionActivoEditDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, padre_id AS PadreId,
                   direccion AS Direccion, responsable AS Responsable, activo AS Activo
              FROM public.fn_af_ubicacion_obtener(@CompanyId, @Id)",
            new { CompanyId = EnsureCompanyId(), Id = id }, TransaccionActual(), cancellationToken: ct));
    }

    public async Task<UbicacionActivoEditDto> GuardarUbicacionAsync(UbicacionActivoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var connection = await AbrirConexionAsync(ct);

        var id = await connection.QuerySingleAsync<int?>(new CommandDefinition(@"
            CALL public.sp_af_ubicacion_guardar(
                @p_id, @p_company_id, @p_codigo, @p_nombre, @p_padre_id,
                @p_direccion, @p_responsable, @p_activo, @p_usuario)",
            new
            {
                p_id = dto.Id,
                p_company_id = EnsureCompanyId(),
                p_codigo = dto.Codigo,
                p_nombre = dto.Nombre,
                p_padre_id = dto.PadreId,
                p_direccion = dto.Direccion,
                p_responsable = dto.Responsable,
                p_activo = dto.Activo,
                p_usuario = Usuario(user)
            }, TransaccionActual(), cancellationToken: ct));

        dto.Id = id;
        return dto;
    }

    public async Task DesactivarUbicacionAsync(int id, string user, CancellationToken ct = default)
    {
        var connection = await AbrirConexionAsync(ct);
        await connection.ExecuteAsync(new CommandDefinition(
            "CALL public.sp_af_ubicacion_desactivar(@p_company_id, @p_id, @p_usuario)",
            new { p_company_id = EnsureCompanyId(), p_id = id, p_usuario = Usuario(user) },
            TransaccionActual(), cancellationToken: ct));
    }

    // ── Infraestructura ─────────────────────────────────────────────────────

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
}
