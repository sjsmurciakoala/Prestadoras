using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Clientes;

/// <summary>
/// Configuración del generador del código de cliente (por empresa) más el
/// preview del próximo código que saldría. El generador solo actúa cuando la
/// clave llega vacía al crear el cliente.
/// </summary>
public sealed class CodigoClienteConfigDto
{
    public bool Activo { get; set; } = true;

    [StringLength(5, ErrorMessage = "El prefijo admite máximo 5 caracteres.")]
    [RegularExpression("^[0-9A-Za-z]*$", ErrorMessage = "El prefijo admite solo letras y números.")]
    public string Prefijo { get; set; } = string.Empty;

    [Range(4, 20, ErrorMessage = "La longitud total debe estar entre 4 y 20.")]
    public short Longitud { get; set; } = 9;

    [Range(1, long.MaxValue, ErrorMessage = "El correlativo debe ser mayor que cero.")]
    public long Siguiente { get; set; } = 1;

    /// <summary>Preview del próximo código (no consume el correlativo).</summary>
    public string? ProximoCodigo { get; set; }

    /// <summary>false cuando la empresa aún no tiene fila de configuración.</summary>
    public bool Configurado { get; set; }
}
