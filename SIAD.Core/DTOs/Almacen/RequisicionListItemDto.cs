using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class RequisicionListItemDto
{
    public int Id { get; init; }
    public decimal Numero { get; init; }
    public DateOnly? FechaRequisicion { get; init; }
    public DateOnly? FechaEntrega { get; init; }
    public string? Solicitante { get; init; }
    public string? Departamento { get; init; }
    public string? CodigoArticulo { get; init; }
    public string? Descripcion { get; init; }
    public string? Aplicacion { get; init; }
    public decimal Cantidad { get; init; }
    public decimal PrecioUnitario { get; init; }
    public decimal Total { get; init; }
    public string? Estatus { get; init; }
    public bool Aprobado { get; init; }
    public bool Rechazado { get; init; }
    public bool Descargado { get; init; }

    public string EstadoDescripcion => EstadoRequisicion.Describir(Estatus);
}
