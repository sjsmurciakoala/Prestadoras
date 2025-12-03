using System;

namespace SIAD.Core.DTOs.Contabilidad;

public record PeriodoContableDto(
    long PeriodId,
    string Code,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    DateTime? ClosedAt,
    string? ClosedBy);
