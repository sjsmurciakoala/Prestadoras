using System;

namespace SIAD.Core.DTOs.Contabilidad;

public record PeriodoContableUpsertDto(
    string Code,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string User,
    long? PeriodId = null);
