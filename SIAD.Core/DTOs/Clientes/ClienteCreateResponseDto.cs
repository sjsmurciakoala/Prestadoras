namespace SIAD.Core.DTOs.Clientes;

/// <summary>
/// Respuesta después de crear un cliente
/// </summary>
public class ClienteCreateResponseDto
{
    /// <summary>
    /// ID del cliente creado
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// Código del sistema (clave) generado automáticamente
    /// </summary>
    public string Codigo { get; set; } = string.Empty;
}
