using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class CompraListItemDto
{
    public int Id { get; init; }
    public int? ArticuloId { get; init; }
    public DateOnly? Fecha { get; init; }
    public DateOnly? FechaFactura { get; init; }
    public string? Proveedor { get; init; }
    public string? CodigoArticulo { get; init; }
    public string? Concepto { get; init; }
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Descuento { get; init; }
    public decimal Total { get; init; }
    public decimal? NumeroFactura { get; init; }
    public string? OrdenCompra { get; init; }
    public string? Oficina { get; init; }
    public short TipoCompra { get; init; }
    public decimal? PlazoDias { get; init; }

    public string TipoCompraDescripcion => Almacen.TipoCompra.Describir(TipoCompra);
}
