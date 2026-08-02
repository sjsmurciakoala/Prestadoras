namespace SIAD.Core.DTOs.Proveedores;

public class ProveedorFilterDto
{
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Rtn { get; set; }
    public bool SoloActivos { get; set; }

    /// <summary>Filtra por tipo de proveedor (cod_tipoproveedor). Null = todos los tipos.</summary>
    public int? TipoProveedorId { get; set; }
}
