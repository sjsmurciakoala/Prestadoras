using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

public interface ICuadroTarifarioService
{
    Task<IReadOnlyList<CuadroTarifarioListDto>> GetCuadrosAsync(CancellationToken ct = default);
    Task<CuadroTarifarioCatalogosDto> GetCatalogosAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarCuadroAsync(CuadroTarifarioSaveRequest request, string usuario, CancellationToken ct = default);
    Task<ResponseModelDto> DesactivarCuadroAsync(long cuadroTarifarioId, string usuario, CancellationToken ct = default);

    Task<IReadOnlyList<ReglaTarifariaListDto>> GetReglasAsync(long cuadroTarifarioId, CancellationToken ct = default);
    Task<ResponseModelDto> GuardarReglaAsync(ReglaTarifariaSaveRequest request, string usuario, CancellationToken ct = default);
    Task<ResponseModelDto> EliminarReglaAsync(long reglaTarifariaId, string usuario, CancellationToken ct = default);
}
