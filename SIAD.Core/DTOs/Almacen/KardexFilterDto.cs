using System;

namespace SIAD.Core.DTOs.Almacen;

public sealed class KardexFilterDto
{
    /// <summary>Artículo a consultar (por id/PK). Filtro preferente.</summary>
    public int? ArticuloId { get; set; }

    /// <summary>Código de artículo a consultar (compatibilidad; se usa si no hay ArticuloId).</summary>
    public string CodigoArticulo { get; set; } = string.Empty;
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? TipoTransaccion { get; set; }

    /// <summary>
    /// Bodega a consultar. Si se indica, el kardex y su saldo corrido se limitan
    /// a esa bodega; si es null, incluye todos los movimientos del artículo.
    /// </summary>
    public int? BodegaId { get; set; }
}
