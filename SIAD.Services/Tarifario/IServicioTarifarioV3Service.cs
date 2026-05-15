using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

public interface IServicioTarifarioV3Service
{
    Task<IReadOnlyList<ServicioTarifarioV3ListDto>> GetAsync(string? search, bool? activo, bool? facturableApp, long? tipoServicioId, CancellationToken ct = default);
    Task<ServicioTarifarioV3EditDto?> GetByIdAsync(long servicioId, CancellationToken ct = default);
    Task<ServicioTarifarioV3CatalogosDto> GetCatalogosAsync(CancellationToken ct = default);
    Task<ResponseModelDto> GuardarAsync(ServicioTarifarioV3EditDto request, string usuario, CancellationToken ct = default);
    Task<ResponseModelDto> DesactivarAsync(long servicioId, string usuario, CancellationToken ct = default);
}
