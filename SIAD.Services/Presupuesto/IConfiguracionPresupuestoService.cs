using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Services.Presupuesto;

public interface IConfiguracionPresupuestoService
{
    Task<string> GetNextIdAsync(CancellationToken ct = default);

    Task<string> GetNextIdAsync(string cuentaContable, CancellationToken ct = default);

    Task<IReadOnlyList<ConfiguracionPresupuestoListItemDto>> GetAsync(
        ConfiguracionPresupuestoFilterDto? filtro,
        CancellationToken ct = default);

    Task<PagedResult<ConfiguracionPresupuestoListItemDto>> GetPagedAsync(
        ConfiguracionPresupuestoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default);

    Task<IReadOnlyList<ConfiguracionPresupuestoDetalleListItemDto>> GetDetailsByPresupuestoAsync(
        string idPresupuesto,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoDetalleListItemDto?> GetDetailByIdAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default);

    Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasDestinoTrasladoAsync(
        string idPresupuesto,
        string cuentaOrigen,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoDetalleListItemDto> AddDetailAsync(
        string idPresupuesto,
        ConfiguracionPresupuestoDetalleEditDto dto,
        string user,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoDetalleListItemDto> UpdateDetailAsync(
        string idPresupuesto,
        string cuentaContable,
        ConfiguracionPresupuestoDetalleUpdateDto dto,
        string user,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoEditDto?> GetByIdAsync(
        string idPresupuesto,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoEditDto?> GetByIdAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoEditDto> CreateAsync(
        ConfiguracionPresupuestoEditDto dto,
        string user,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoEditDto> UpdateAsync(
        string idPresupuesto,
        ConfiguracionPresupuestoEditDto dto,
        string user,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoEditDto> UpdateAsync(
        string idPresupuesto,
        string cuentaContable,
        ConfiguracionPresupuestoEditDto dto,
        string user,
        CancellationToken ct = default);

    Task<ConfiguracionPresupuestoEditDto> ApprovePresupuestoAsync(
        string idPresupuesto,
        string user,
        CancellationToken ct = default);

    Task<bool> DeleteAsync(string idPresupuesto, string cuentaContable, CancellationToken ct = default);

    Task<IReadOnlyList<PresupuestoActividadSolicitudListItemDto>> GetSolicitudesByDetalleAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default);

    Task<PresupuestoActividadSolicitudListItemDto> CreateSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        PresupuestoActividadSolicitudCreateDto dto,
        string user,
        CancellationToken ct = default);

    Task<PresupuestoActividadSolicitudListItemDto> ApproveSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        string user,
        string? comentario,
        CancellationToken ct = default);

    Task<PresupuestoActividadSolicitudListItemDto> RejectSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        string user,
        string? comentario,
        CancellationToken ct = default);
}
