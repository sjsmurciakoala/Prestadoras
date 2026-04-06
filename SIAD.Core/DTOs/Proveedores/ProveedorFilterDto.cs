namespace SIAD.Core.DTOs.Proveedores;

public class ProveedorFilterDto
{
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Rtn { get; set; }
    public bool SoloActivos { get; set; }
}
