using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de impuestos (SAR Honduras). Ej: ISV.
/// <para>
/// ÁMBITO GLOBAL, a propósito: las tasas del ISV las fija la ley, no la empresa.
/// Por eso NO implementa <c>ICompanyScopedEntity</c> ni lleva <c>company_id</c> —
/// mismo patrón que <c>cfg_tipo_documento_fiscal</c> / <c>cfg_motivo_anulacion</c>.
/// Lo que sí es por empresa es qué tasa lleva cada artículo, y eso vive en las
/// tablas multiempresa, no aquí.
/// </para>
/// </summary>
public partial class cfg_impuesto
{
    public int id { get; set; }

    /// <summary>Código corto del impuesto. Único. Ej: ISV.</summary>
    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? descripcion { get; set; }

    public bool activo { get; set; } = true;

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<cfg_impuesto_tasa> tasas { get; set; } = new List<cfg_impuesto_tasa>();
}
