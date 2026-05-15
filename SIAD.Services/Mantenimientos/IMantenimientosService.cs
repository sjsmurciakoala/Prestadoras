using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Mantenimientos;

namespace SIAD.Services.Mantenimientos;

public interface IMantenimientosService
{
    Task<RecargoMoraDto> ObtenerRecargoMoraAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarRecargoMoraAsync(RecargoMoraDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<AjusteTarifarioDto>> ListarAjustesTarifariosAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarAjusteTarifarioAsync(AjusteTarifarioSaveRequestDto dto, CancellationToken ct = default);
}
