using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Unificación cobranza F6 (2026-07-29): la cuota es un DOCUMENTO cobrable por
// la caja única (documento_tipo = 2 en adm_pago_aplicacion). estado_id usa el
// mismo catálogo que la factura (cfg_estado_documento_comercial: 1 Activa,
// 4 Abonada parcial, 2 Cobrada, 3 Anulada) y saldo_cuota es el saldo vivo que
// el motor rebaja al aplicar pagos (análogo de factura_detalle.montovalor_saldo).
public partial class cln_plan_pago_dtl : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public short estado_id { get; set; } = 1;

    public decimal saldo_cuota { get; set; }
}
