using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed class OrdenTrabajoFilterDto
{
    public string? Busqueda { get; set; }
    public string? Estado { get; set; }
    public string? Tipo { get; set; }
    public string? ClienteClave { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public int? Anio { get; set; }
    public int? Mes { get; set; }
    public string? Departamento { get; set; }
}
