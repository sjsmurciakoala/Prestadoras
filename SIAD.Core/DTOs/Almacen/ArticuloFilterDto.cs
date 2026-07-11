namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloFilterDto
{
    public string? Search { get; set; }
    public string? Linea { get; set; }
    public bool? SoloBajoMinimo { get; set; }
}
