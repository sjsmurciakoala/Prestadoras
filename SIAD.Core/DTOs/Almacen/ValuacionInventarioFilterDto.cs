using System;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Filtro del reporte "Valuación de inventario a una fecha": reconstruye el saldo por
/// (artículo, bodega) tal como estaba a la <see cref="FechaCorte"/>, leyendo el último asiento
/// del kardex con snapshot (existencia/costo resultante) hasta esa fecha.
/// </summary>
public sealed class ValuacionInventarioFilterDto
{
    /// <summary>Fecha de corte (inclusive). El inventario se valúa como estaba a esta fecha. Null = hoy.</summary>
    public DateOnly? FechaCorte { get; set; }

    /// <summary>Acota a una bodega. Null = todas.</summary>
    public int? BodegaId { get; set; }

    /// <summary>Acota a un tipo de artículo. Null = todos.</summary>
    public int? TipoArticuloId { get; set; }

    /// <summary>Busca por código o descripción del artículo. Null/vacío = todos.</summary>
    public string? Search { get; set; }
}
