namespace SIAD.Core.DTOs.Solicitudes;

/// <summary>
/// DTO para actualizar una solicitud de servicio existente.
/// </summary>
public class SolicitudUpdateDto
{
    public int Id { get; set; }
    public string IdentificacionCliente { get; set; } = "";
    public string NombreCliente { get; set; } = "";
    public int CategoriaServicioId { get; set; }
    public string Telefono { get; set; } = "";
    public string Movil { get; set; } = "";
    public string Direccion { get; set; } = "";
    public string? Rtn { get; set; }
    public string? Correo { get; set; }
    public string? Observacion { get; set; }
    public string? ColorCasa { get; set; }
    public DateTime? FechaNacimiento { get; set; }
    public string? ClaveSure { get; set; }
    public string? EmpresaNombre { get; set; }
    public string? EmpresaTelefono { get; set; }
    public string? EmpresaDireccion { get; set; }
    public string? NegocioNombre { get; set; }
    public string? NegocioTelefono { get; set; }
    public string? NegocioClaveCatastral { get; set; }
}
