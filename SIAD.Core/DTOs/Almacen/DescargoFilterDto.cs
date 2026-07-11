using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class DescargoFilterDto
{
    public string? Search { get; set; }
    public string? Departamento { get; set; }
    public decimal? NumeroRequisicion { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}
