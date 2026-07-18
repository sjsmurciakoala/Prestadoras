using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;

namespace SIAD.Services.Tarifario;

/// <summary>
/// Mantenimiento de los porcentajes con que los abonos se distribuyen entre los
/// ítems del desglose por servicio del estado de cuenta del cliente.
/// </summary>
public interface IDesgloseAbonoConfigService
{
    Task<IReadOnlyList<DesgloseAbonoItemDto>> GetAsync(CancellationToken ct = default);

    Task<ResponseModelDto> GuardarAsync(DesgloseAbonoGuardarDto dto, string usuario, CancellationToken ct = default);
}
