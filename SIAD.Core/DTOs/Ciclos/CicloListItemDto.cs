namespace SIAD.Core.DTOs.Ciclos;

public sealed class CicloListItemDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string DescripcionCorta { get; set; } = string.Empty;
    public string DescripcionLarga { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
