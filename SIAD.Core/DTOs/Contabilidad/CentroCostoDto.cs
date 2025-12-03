namespace SIAD.Core.DTOs.Contabilidad;

public record CentroCostoDto(
    long CostCenterId,
    string Code,
    string Name,
    string? Description,
    string Status);
