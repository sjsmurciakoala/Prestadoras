namespace SIAD.Core.DTOs.Clientes;

public class ClienteFilterDto
{
    public string? Codigo { get; set; }
    public string? Nombre { get; set; }
    public string? Barrio { get; set; }
    public bool SoloActivos { get; set; }
}
