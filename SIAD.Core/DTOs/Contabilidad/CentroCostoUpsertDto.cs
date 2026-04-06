using System;

namespace SIAD.Core.DTOs.Contabilidad;

public record CentroCostoUpsertDto(
    string Code,
    string Name,
    string Status,
    string User,
    long? CostCenterId = null,
    string? Description = null,
    bool? AllowsMovement = null,
    bool? IsPeriodic = null,
    DateTime? StartDate = null,
    DateTime? EndDate = null,
    bool? LegacyStatus = null,
    short? LegacyTypeTrans = null,
    string? LegacyParentCode = null,
    int? LegacyKeyCost = null,
    string? LegacyNotes = null);
