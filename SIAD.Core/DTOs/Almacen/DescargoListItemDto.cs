using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class DescargoListItemDto
{
    public int Id { get; init; }
    public int? ArticuloId { get; init; }
    public DateOnly? Fecha { get; init; }
    public string? CodigoArticulo { get; init; }
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Total { get; init; }
    public string? Departamento { get; init; }
    public decimal? NumeroRequisicion { get; init; }
    public decimal? NumeroDocumento { get; init; }
    public string? Comentario { get; init; }
    public string? Oficina { get; init; }
}
