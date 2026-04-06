using System;

namespace SIAD.Core.DTOs.Contabilidad;

public record CentroCostoDto(
    long CostCenterId,
    string Code,
    string Name,
    string? Description,
    string Status,
    bool AllowsMovement,
    bool IsPeriodic,
    DateTime? StartDate,
    DateTime? EndDate,
    bool LegacyStatus,
    short LegacyTypeTrans,
    string? LegacyParentCode,
    int? LegacyKeyCost,
    string? LegacyNotes);
