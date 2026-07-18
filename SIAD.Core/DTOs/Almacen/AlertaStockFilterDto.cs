namespace SIAD.Core.DTOs.Almacen;

public sealed class AlertaStockFilterDto
{
    public string? Search { get; set; }

    /// <summary>Filtra por tipo de artículo (alm_tipo_articulo). Null = todos.</summary>
    public int? TipoArticuloId { get; set; }

    /// <summary>Filtra por severidad: "Negativa", "SinStock" o "BajoMinimo". Null = todas.</summary>
    public string? Severidad { get; set; }

    /// <summary>Si true, sólo artículos con existencia mínima definida (&gt; 0).</summary>
    public bool? SoloConMinimo { get; set; }
}
