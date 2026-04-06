using System;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Contabilidad;

public record PeriodoContableUpsertDto(
    string Code,
    string Name,
    DateTime StartDate,
    DateTime EndDate,
    string Status,
    string User,
    long? PeriodId = null,
    short StatusId = EstadoPeriodoHelper.AbiertoId);
