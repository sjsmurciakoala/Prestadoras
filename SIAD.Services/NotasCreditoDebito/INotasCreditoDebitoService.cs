using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;

namespace SIAD.Services.NotasCreditoDebito;

public interface INotasCreditoDebitoService
{
    Task<IReadOnlyList<NotaClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default);
    Task<IReadOnlyList<FacturaOrigenLookupDto>> BuscarFacturasClienteAsync(string clienteClave, CancellationToken ct = default);
    Task<IReadOnlyList<MotivoLookupDto>> ListarMotivosAnulacionAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MotivoLookupDto>> ListarMotivosAumentoAsync(CancellationToken ct = default);
    Task<IReadOnlyList<CaiNotaLookupDto>> ListarCaisNotaAsync(short tipoDocumentoFiscalId, CancellationToken ct = default);
    Task<EmitirNotaResponseDto> EmitirNotaCreditoAsync(EmitirNotaCreditoRequestDto dto, CancellationToken ct = default);
    Task<EmitirNotaResponseDto> EmitirNotaDebitoAsync(EmitirNotaDebitoRequestDto dto, CancellationToken ct = default);
    Task<PagedResult<NotaEmitidaListDto>> ListarNotasEmitidasPagedAsync(
        NotaEmitidaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);

    // Mantenimiento de catálogos de motivos
    Task<IReadOnlyList<MotivoCrudDto>> ListarMotivosAnulacionCrudAsync(CancellationToken ct = default);
    Task<IReadOnlyList<MotivoCrudDto>> ListarMotivosAumentoCrudAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarMotivoAnulacionAsync(MotivoSaveRequestDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> GuardarMotivoAumentoAsync(MotivoSaveRequestDto dto, CancellationToken ct = default);
}
