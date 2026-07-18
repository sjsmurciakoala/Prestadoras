namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloFilterDto
{
    public string? Search { get; set; }

    /// <summary>Filtra por tipo de artículo (alm_tipo_articulo). Null = todos.</summary>
    public int? TipoArticuloId { get; set; }

    public bool? SoloBajoMinimo { get; set; }
}
