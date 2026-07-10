using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Informes;

public sealed class PartidasInformeFiltroDto
{
    public long? PeriodId { get; set; }

    public long? JournalId { get; set; }

    public long? TypeId { get; set; }

    public short? Status { get; set; }

    public DateTime? FechaDesde { get; set; }

    public DateTime? FechaHasta { get; set; }

    public string? Search { get; set; }

    public int Skip { get; set; } = 0;

    public int Take { get; set; } = 100;
}

public sealed record PartidasInformeItemDto(
    long PolizaId,
    string PolizaNumber,
    DateTime PolizaDate,
    string? PeriodoCode,
    string? DiarioCode,
    string? TipoCode,
    string? TipoNombre,
    string Module,
    string DocumentType,
    string? DocumentNumber,
    string? SourceReference,
    string? Description,
    short Status,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record PartidasInformeResultadoDto(
    IReadOnlyList<PartidasInformeItemDto> Items,
    int TotalCount,
    decimal TotalDebit,
    decimal TotalCredit
);
