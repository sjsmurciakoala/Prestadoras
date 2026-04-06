namespace SIAD.Core.DTOs.TarifasBase;

public sealed class TarifaBaseListItemDto
{
    public int Tipo { get; set; }
    public int CategoriaId { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string? Descripcion { get; set; }
    public decimal? Valor { get; set; }

    public string Key => $"{Tipo}-{CategoriaId}-{Codigo}";
}
