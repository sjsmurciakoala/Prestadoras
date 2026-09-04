using System;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Filtro del "libro de movimientos de bodega": todos los asientos de kardex de UNA bodega
/// (todos los artículos) en un período. A diferencia de <see cref="KardexFilterDto"/>, el
/// eje es la bodega, no el artículo.
/// </summary>
public sealed class MovimientosBodegaFilterDto
{
    /// <summary>Bodega a consultar (obligatoria). Sin ella no hay libro que mostrar.</summary>
    public int? BodegaId { get; set; }

    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }

    /// <summary>Tipo de transacción del kardex (código). Null/vacío = todos.</summary>
    public string? TipoTransaccion { get; set; }

    /// <summary>Acota a un artículo dentro de la bodega. Null = todos los artículos.</summary>
    public int? ArticuloId { get; set; }

    /// <summary>Busca en código/descripción del artículo o en la descripción del asiento.</summary>
    public string? Search { get; set; }
}
