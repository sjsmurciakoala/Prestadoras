using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Configuración de correlativos/numeradores para documentos
/// </summary>
public class con_configuracion_correlativo : ICompanyScopedEntity
{
    public long config_correlativo_id { get; set; }
    
    public long company_id { get; set; }

    public string tipo { get; set; } = null!; // Facturas, Notas, Comprobantes, Asientos, etc.

    public string numerador { get; set; } = null!; // Serie o prefijo

    public int siguiente_numero { get; set; } = 1;

    public string? formato { get; set; } // Ej: {NUMERADOR}-{YYYY}-{XXXXXX}

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
