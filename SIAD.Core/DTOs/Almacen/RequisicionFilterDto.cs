using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class RequisicionFilterDto
{
    public string? Search { get; set; }
    public string? Estatus { get; set; }
    public string? Departamento { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}
