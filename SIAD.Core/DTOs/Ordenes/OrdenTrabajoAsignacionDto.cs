using System.Collections.Generic;

namespace SIAD.Core.DTOs.Ordenes;

public sealed class OrdenTrabajoAsignacionDto
{
    public IList<int> NumerosOrden { get; set; } = new List<int>();
    public string Usuario { get; set; } = string.Empty;
    public string? Estado { get; set; }
    public string? Empleado { get; set; }
}
