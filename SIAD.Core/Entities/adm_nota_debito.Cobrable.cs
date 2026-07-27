namespace SIAD.Core.Entities;

// Unificación cobranza F7 H2b (2026-07-30): la ND es un DOCUMENTO COBRABLE
// por la caja única (documento_tipo = 3). saldo_pendiente es el saldo vivo
// que el motor rebaja al aplicar pagos (análogo de montovalor_saldo /
// saldo_cuota); "cobrada" = 0. El estado fiscal (estado_id contra
// cfg_estado_documento_fiscal) NO cambia por cobros.
public partial class adm_nota_debito
{
    public decimal saldo_pendiente { get; set; }
}
