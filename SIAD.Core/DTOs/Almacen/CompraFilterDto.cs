using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class CompraFilterDto
{
    public string? Search { get; set; }
    public string? Proveedor { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public short? TipoCompra { get; set; }
}
