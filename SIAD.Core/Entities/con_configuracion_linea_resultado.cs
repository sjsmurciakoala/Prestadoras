using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Configuración de líneas del Estado de Resultados
/// Equivalente a C01BlProfLoss en sistema de referencia
/// </summary>
public class con_configuracion_linea_resultado : ICompanyScopedEntity
{
    public long config_linea_id { get; set; }

    public long company_id { get; set; }

    public long periodo_id { get; set; }

    public short numero_linea { get; set; }  // Orden en el estado de resultados

    public byte tipo_linea { get; set; }  // 0=Ingreso, 1=Costo, 2=Gasto

    public string? codigo_cuenta { get; set; }

    public string? descripcion_linea { get; set; }

    public byte? columna_reporte { get; set; }

    public bool mostrar_subtotal { get; set; } = false;

    public byte nivel_indentacion { get; set; } = 0;  // 0, 1, 2... para sangría

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    // Navigation
    public virtual con_periodo_contable? periodo { get; set; }

    public virtual con_plan_cuenta? cuenta { get; set; }
}
