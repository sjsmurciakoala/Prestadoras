using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de retenciones a proveedores (SAR Honduras). Ej: ISR 12.5%, ISR 1%, ISV retenido.
/// <para>
/// ÁMBITO GLOBAL, a propósito: los porcentajes de retención los fija la ley (SAR), no la
/// empresa. Por eso NO implementa <c>ICompanyScopedEntity</c> ni lleva <c>company_id</c> —
/// mismo patrón que <see cref="cfg_impuesto"/>. Lo que SÍ es por empresa es la cuenta contable
/// del pasivo ("retenciones por pagar"), y eso vive en <see cref="prv_retencion_cuenta"/>.
/// </para>
/// </summary>
public partial class cfg_retencion
{
    public int id { get; set; }

    /// <summary>Código corto de la retención. Único. Ej: ISR-SERV, ISR-PROV, ISV-RET.</summary>
    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? descripcion { get; set; }

    /// <summary>
    /// Base de cálculo: <c>TOTAL</c> (monto con ISV) | <c>SIN_ISV</c> (subtotal). Ver
    /// <c>SIAD.Core.Constants.BaseRetencion</c>. Se llama <c>base_calculo</c> y no <c>base</c>
    /// porque <c>base</c> es palabra reservada de C#.
    /// </summary>
    public string base_calculo { get; set; } = null!;

    /// <summary>Impuesto que se retiene: <c>ISR</c> | <c>ISV</c>. Ver <c>SIAD.Core.Constants.TipoImpuestoRetencion</c>.</summary>
    public string tipo_impuesto { get; set; } = null!;

    public bool activo { get; set; } = true;

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<cfg_retencion_tasa> tasas { get; set; } = new List<cfg_retencion_tasa>();
}
