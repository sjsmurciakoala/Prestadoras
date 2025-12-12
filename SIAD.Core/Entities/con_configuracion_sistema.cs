using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Configuraci�n unificada del sistema contable por empresa
/// Consolidaci�n de configuraci�n principal y cuentas de utilidad
/// </summary>
public class con_configuracion_sistema : ICompanyScopedEntity
{
    public long config_id { get; set; }

    public long company_id { get; set; }

    // ===== CONFIGURACI�N PRINCIPAL =====
    public DateTime? fecha_ini_ejer { get; set; }

    public DateTime? fecha_fin_ejer { get; set; }

    public int meses_calc { get; set; }

    public string sep_codigo { get; set; } = "-";

    public string fmt_ctas { get; set; } = "###-###-##";

    public string fmt_centros { get; set; } = "###-##";

    public string sym_acreedor { get; set; } = "CR";

    public decimal? mto_maximo { get; set; }

    public string frec_deprec { get; set; } = "Mensual";

    public DateTime? fec_ult_deprec { get; set; }

    // ===== CUENTAS DE UTILIDAD - HIST�RICAS (como c�digos, no IDs) =====
    public string? cod_util_acum_hist { get; set; }

    public string? cod_util_ejer_hist { get; set; }

    public string? cod_perd_acum_hist { get; set; }

    public string? cod_perd_ejer_hist { get; set; }

    // ===== CUENTAS DE UTILIDAD - INFLACI�N (como c�digos, no IDs) =====
    public string? cod_util_acum_inf { get; set; }

    public string? cod_util_ejer_inf { get; set; }

    public string? cod_perd_acum_inf { get; set; }

    public string? cod_perd_ejer_inf { get; set; }

    // ===== OPCIONES DE PRESENTACI�N =====
    public bool mostrar_orden { get; set; }

    public bool mostrar_percontra { get; set; }

    // ===== T�TULOS Y DESCRIPCIONES =====
    public string tit_est_result { get; set; } = "Estado de Resultados";

    public string tit_balance_gral { get; set; } = "Balance General";

    public string descripcion_activo { get; set; } = "ACTIVO";

    public string descripcion_pasivo { get; set; } = "PASIVO";

    public string descripcion_capital { get; set; } = "CAPITAL";

    public string desc_pasiv_cap { get; set; } = "PASIVO y CAPITAL";

    public string desc_orden { get; set; } = "CUENTAS ORDEN";

    // ===== AUDITOR�A =====
    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    // Navigation
    public virtual con_empresa_configuracion? empresa_configuracion { get; set; }
}
