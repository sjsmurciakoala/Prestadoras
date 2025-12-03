using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Configuración de líneas del Balance General (Estado de Situación Financiera)
/// Equivalente a C01BlSheet en sistema de referencia
/// </summary>
public class con_configuracion_balance : ICompanyScopedEntity
{
    public long config_balance_id { get; set; }

    public long company_id { get; set; }

    public long periodo_id { get; set; }

    public short numero_linea { get; set; }  // Orden en el balance

    public byte clase { get; set; }  // 1-8: Activo CP, Activo LP, Pasivo CP, Pasivo LP, Capital, etc.

    public string? codigo_cuenta { get; set; }

    public string? descripcion_cuenta { get; set; }

    public string? descripcion_linea { get; set; }

    public decimal? porcentaje_activo { get; set; }

    public bool mostrar_en_reporte { get; set; } = true;

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    // Navigation
    public virtual con_periodo_contable? periodo { get; set; }

    public virtual con_plan_cuenta? cuenta { get; set; }
}
