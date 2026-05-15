using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

public interface ICaiTarifarioService
{
    Task<IReadOnlyList<CaiFacturacionListDto>> GetCaisAsync(CancellationToken ct = default);
    Task<PagedResult<CaiFacturacionListDto>> GetCaisPagedAsync(CaiFacturacionFilterDto filter, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);
    Task<IReadOnlyList<CaiBloqueReservadoListDto>> GetBloquesAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarCaiAsync(CaiFacturacionSaveRequest request, string usuario, CancellationToken ct = default);
    Task<ResponseModelDto> ReservarBloqueAsync(CaiBloqueReservadoSaveRequest request, string usuario, CancellationToken ct = default);

    // SAR-Compliance lookups para los combos en la UI
    Task<IReadOnlyList<TipoDocumentoFiscalLookupDto>> GetTiposDocumentoFiscalLookupAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CaiEstadoLookupDto>> GetCaiEstadosLookupAsync(CancellationToken ct = default);

    // Toggle rapido de estado del CAI desde el grid (anular/reactivar/etc).
    Task<ResponseModelDto> CambiarEstadoAsync(long caiId, short estadoId, string usuario, CancellationToken ct = default);
}
