using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Condición de lectura administrable POR EMPRESA (spec §2, 2026-07-06, A6).
/// El admin crea/renombra/desactiva condiciones y elige a qué <see cref="tipo"/>
/// del catálogo global (<see cref="adm_condicion_lectura_tipo"/>) se comporta;
/// nunca inventa un comportamiento desconocido para el motor V3. La expone
/// GET /api/condiciones (apc.MobileApi) scopeada por la sesión.
/// </summary>
public partial class adm_condicion_lectura : ICompanyScopedEntity
{
    public long condicion_lectura_id { get; set; }

    public long company_id { get; set; }

    /// <summary>Código visible al lector (editable).</summary>
    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    /// <summary>Tipo del catálogo global — define el cálculo del motor (no editable como texto libre).</summary>
    public string tipo { get; set; } = null!;

    /// <summary>'S'/'N' — la lectura se factura.</summary>
    public string facturacion { get; set; } = "S";

    /// <summary>'S'/'N' — aplica descuento.</summary>
    public string aplica_descuento { get; set; } = "N";

    public int orden { get; set; }

    public bool activo { get; set; } = true;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
