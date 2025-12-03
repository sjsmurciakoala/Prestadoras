using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Líneas del Estado de Resultados configurables
/// </summary>
public class con_configuracion_estado_resultado : ICompanyScopedEntity
{
    public long config_resultado_id { get; set; }
    
    public long company_id { get; set; }

    public string tipo { get; set; } = "Ingreso"; // Ingreso, Costo, Gasto

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public int orden { get; set; }

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
