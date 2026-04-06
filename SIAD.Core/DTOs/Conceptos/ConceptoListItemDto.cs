namespace SIAD.Core.DTOs.Conceptos;

public sealed class ConceptoListItemDto
{
    public int Id { get; set; }
    public string Depto { get; set; } = string.Empty;
    public string Tipo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public string Concepto { get; set; } = string.Empty;
    public string DeptoAppMiTrabajo { get; set; } = string.Empty;
    public bool Activo { get; set; }
}
