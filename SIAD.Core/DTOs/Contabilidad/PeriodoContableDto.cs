using System;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Contabilidad;

public record PeriodoContableDto(
    long PeriodId,
    string Code,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    DateTime? ClosedAt,
    string? ClosedBy,
    short StatusId = EstadoPeriodoHelper.AbiertoId);
