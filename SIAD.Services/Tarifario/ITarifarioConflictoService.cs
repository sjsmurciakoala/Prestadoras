using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

public interface ITarifarioConflictoService
{
    Task<IReadOnlyList<TarifarioConflictoListDto>> GetAsync(
        string? search,
        string? estadoCodigo,
        string? rutaCodigo,
        int? clienteId,
        CancellationToken ct = default);

    Task<ResponseModelDto> ResolverAsync(
        TarifarioConflictoResolveRequest request,
        string usuario,
        CancellationToken ct = default);
}
