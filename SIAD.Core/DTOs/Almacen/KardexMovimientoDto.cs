using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class KardexMovimientoDto
{
    public int Id { get; init; }
    public DateOnly? Fecha { get; init; }
    public decimal? NumeroDocumento { get; init; }
    public string? TipoTransaccion { get; init; }
    public string TipoDescripcion => TipoMovimientoKardex.Describir(TipoTransaccion);
    public string? Descripcion { get; init; }
    public string? Departamento { get; init; }
    public int? BodegaId { get; init; }
    public string? BodegaCodigo { get; init; }
    public string? BodegaNombre { get; init; }
    public decimal Ingresos { get; init; }
    public decimal Salidas { get; init; }
    public decimal ValorUnitario { get; init; }
    public decimal Total { get; init; }

    /// <summary>Saldo corrido (Σ ingresos − Σ salidas hasta este movimiento).</summary>
    public decimal Saldo { get; set; }
}
