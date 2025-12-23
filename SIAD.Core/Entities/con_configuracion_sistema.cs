using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Configuración unificada del sistema contable por empresa
/// Consolidación de configuración principal y cuentas de utilidad
/// </summary>
public class con_configuracion_sistema : ICompanyScopedEntity
{
    public long config_id { get; set; }

    public long company_id { get; set; }

    // ===== CONFIGURACIÓN PRINCIPAL =====
    public DateTime? fecha_inicio_ejercicio { get; set; }

    public DateTime? fecha_fin_ejercicio { get; set; }

    public int meses_calculados { get; set; }

    public string separador_codigo { get; set; } = "-";

    public string formato_cuentas { get; set; } = "###-###-##";

    public string formato_centros { get; set; } = "###-##";

    public string symbol_saldo_acreedor { get; set; } = "CR";

    public decimal? monto_maximo { get; set; }

    public string frecuencia_depreciacion { get; set; } = "Mensual";

    public DateTime? ultima_depreciacion { get; set; }

    // ===== CUENTAS DE UTILIDAD - HISTÓRICAS (como códigos, no IDs) =====
    public string? codigo_cuenta_util_acumulada_historica { get; set; }

    public string? codigo_cuenta_util_ejercicio_historica { get; set; }

    public string? codigo_cuenta_perdida_acumulada_historica { get; set; }

    public string? codigo_cuenta_perdida_ejercicio_historica { get; set; }

    // ===== CUENTAS DE UTILIDAD - INFLACIÓN (como códigos, no IDs) =====
    public string? codigo_cuenta_util_acumulada_inflacion { get; set; }

    public string? codigo_cuenta_util_ejercicio_inflacion { get; set; }

    public string? codigo_cuenta_perdida_acumulada_inflacion { get; set; }

    public string? codigo_cuenta_perdida_ejercicio_inflacion { get; set; }

    // ===== OPCIONES DE PRESENTACIÓN =====
    public bool mostrar_orden { get; set; }

    public bool mostrar_percontra { get; set; }

    // ===== TÍTULOS Y DESCRIPCIONES =====
    public string titulo_estado_resultados { get; set; } = "Estado de Resultados";

    public string titulo_balance_general { get; set; } = "Balance General";

    public string descripcion_activo { get; set; } = "ACTIVO";

    public string descripcion_pasivo { get; set; } = "PASIVO";

    public string descripcion_capital { get; set; } = "CAPITAL";

    public string descripcion_pasivo_capital { get; set; } = "PASIVO y CAPITAL";

    public string descripcion_orden { get; set; } = "CUENTAS ORDEN";

    // ===== AUDITORÍA =====
    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    // Navigation
    public virtual con_empresa_configuracion? empresa_configuracion { get; set; }
}
