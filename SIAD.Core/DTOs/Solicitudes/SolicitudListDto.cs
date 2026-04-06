namespace SIAD.Core.DTOs.Solicitudes;

/// <summary>
/// DTO para listado de solicitudes de servicio.
/// </summary>
public class SolicitudListDto
{
    public int Id { get; set; }
    public string IdentificacionCliente { get; set; } = "";
    public string NombreCliente { get; set; } = "";
    public int CategoriaServicioId { get; set; }
    public string CategoriaServicioNombre { get; set; } = "";
    public DateTime Fecha { get; set; }
    public bool Estado { get; set; }
    public bool Asignada { get; set; }
}
