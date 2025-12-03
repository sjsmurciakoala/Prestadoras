using System;

namespace SIAD.Core.DTOs.Ordenes;

public sealed class CrearOrdenTrabajoDto
{
    public string ClienteClave { get; set; } = string.Empty;
    public DateTime Fecha { get; set; } = DateTime.UtcNow;
    public string? Personas { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public string Estado { get; set; } = "P";
    public string? Empleado { get; set; }
    public string Usuario { get; set; } = string.Empty;
    public int? Anio { get; set; }
    public int? Mes { get; set; }
    public decimal? Saldo { get; set; }
    public int? Numero { get; set; }
}
