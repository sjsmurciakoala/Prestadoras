using System;
using System.Collections.Generic;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cuenta por pagar de una factura de compra (1:1 con <see cref="alm_compra_hdr"/>). Es un libro
/// auxiliar de saldos por proveedor: toda factura —contado o crédito— genera una, y todas se
/// pagan desde la misma vista. Independiente de la contabilidad (el asiento de nacimiento es una
/// fase aparte). El saldo se materializa y se mantiene con cada abono bajo lock.
/// </summary>
public partial class alm_compra_cxp : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Factura que origina la CxP. Único por empresa (una CxP por factura).</summary>
    public int compra_hdr_id { get; set; }

    public string cod_proveedor { get; set; } = null!;
    public string? proveedor { get; set; }
    public string? numero_factura_sar { get; set; }

    public DateOnly fecha { get; set; }
    public DateOnly fecha_vencimiento { get; set; }

    /// <summary>Ver <see cref="CondicionPagoCompra"/> (1 Contado · 2 Crédito · 3 Prepagado).</summary>
    public short condicion_pago { get; set; } = CondicionPagoCompra.Contado;

    public decimal monto { get; set; }

    /// <summary>Saldo pendiente = monto − Σ abonos vigentes. Materializado; se recalcula con cada abono.</summary>
    public decimal saldo { get; set; }

    /// <summary>Ver <see cref="EstadoCompraCxp"/> (1 Pendiente · 2 Parcial · 3 Pagada · 9 Anulada).</summary>
    public short estado_id { get; set; } = EstadoCompraCxp.Pendiente;

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_compra_hdr? compra { get; set; }
    public virtual ICollection<alm_compra_cxp_abono> abonos { get; set; } = new List<alm_compra_cxp_abono>();
}
