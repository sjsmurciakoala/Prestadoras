using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Aplicación del pago por documento (y por línea de factura cuando aplica).
// Invariante del motor: SUM(monto_aplicado) por pago = adm_pago.monto_total.
// Unificación cobranza F2 (2026-07-26), plan §3.5.
public partial class adm_pago_aplicacion : ICompanyScopedEntity
{
    public long aplicacion_id { get; set; }

    public long company_id { get; set; }

    public long pago_id { get; set; }

    /// <summary>1 = factura, 2 = cuota de plan (F6), 3 = nota de débito (constantes DocumentoCobroTipo).</summary>
    public short documento_tipo { get; set; }

    public int? factura_id { get; set; }

    public int? factura_detalle_id { get; set; }

    public int? plan_cuota_id { get; set; }

    public long? nota_debito_id { get; set; }

    public decimal monto_aplicado { get; set; }

    public virtual adm_pago pago { get; set; } = null!;
}
