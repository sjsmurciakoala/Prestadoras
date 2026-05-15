using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TiposDocumentoFiscal;

namespace SIAD.Services.TiposDocumentoFiscal;

public interface ITiposDocumentoFiscalService
{
    Task<IReadOnlyList<TipoDocumentoFiscalDto>> ListarAsync(CancellationToken ct = default);
    Task<ResponseModelDto> ActualizarAsync(short id, TipoDocumentoFiscalUpdateDto dto, string usuario, CancellationToken ct = default);
}
