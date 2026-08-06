using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Renglón de una orden de compra. Una fila por artículo pedido. La recepción
/// (alm_compra) incrementa <see cref="cantidad_aplicada"/>; cuando todos los
/// renglones quedan cubiertos, la O/C pasa a Cerrada. Multiempresa vía ICompanyScopedEntity.
/// </summary>
public partial class alm_orden_compra_detalle : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int orden_compra_id { get; set; }
    public int articulo_id { get; set; }

    /// <summary>Snapshot del código de proveedor del artículo (alm_articulo_proveedor.codigo_upc).</summary>
    public string? codigo_upc { get; set; }

    /// <summary>Centro de costo (informativo). No valida presupuesto (decisión usuario 2026-07-30).</summary>
    public string? centro_costo { get; set; }

    public string? descripcion { get; set; }
    public decimal cantidad_pedida { get; set; }
    public decimal costo_unitario { get; set; }
    public decimal impuesto { get; set; }
    public decimal total { get; set; }

    /// <summary>Cantidad ya recibida contra este renglón. La incrementa la recepción.</summary>
    public decimal cantidad_aplicada { get; set; }

    public virtual alm_orden_compra? orden { get; set; }
    public virtual alm_articulo? articulo { get; set; }
}
