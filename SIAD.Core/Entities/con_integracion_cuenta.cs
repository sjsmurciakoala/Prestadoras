using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Matriz de cuentas de integración contable (plan 2026-07-02 F1).
/// Mapea (uso × servicio × categoría × medición) → cuenta del plan de la
/// empresa. NULL en una dimensión = comodín; fn_con_resolver_cuenta elige
/// la fila más específica.
/// </summary>
public partial class con_integracion_cuenta : ICompanyScopedEntity
{
    public long integracion_cuenta_id { get; set; }

    public long company_id { get; set; }

    public string uso { get; set; } = null!;

    public long? servicio_id { get; set; }

    public int? categoria_servicio_id { get; set; }

    public bool? con_medicion { get; set; }

    public long account_id { get; set; }

    public bool is_active { get; set; } = true;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_plan_cuenta? account { get; set; }
}
