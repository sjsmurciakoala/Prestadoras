namespace SIAD.Core.DTOs.Contabilidad;

public record CentroCostoUpsertDto(
    string Code,
    string Name,
    string Status,
    string User,
    long? CostCenterId = null,
    string? Description = null);
