namespace SIAD.Core.DTOs.Servicios;

public sealed class ServicioListItemDto
{
    public int Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string DescripcionCorta { get; set; } = string.Empty;
    public string? DescripcionLarga { get; set; }
    public bool Activo { get; set; }
    public bool FacturableApp { get; set; }
    public int AppOrden { get; set; }
    public string? AppGrupo { get; set; }
    public long? CuentaContableId { get; set; }
    public string? CuentaContableCodigo { get; set; }
    public string? CuentaContableNombre { get; set; }
}
