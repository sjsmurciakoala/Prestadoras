namespace SIAD.Core.Entities;

// Espejos numéricos (unificación cobranza F1, 2026-07). Los mantiene el
// trigger trg_transaccion_abonado_sync_estado_id en BD; el código C# los LEE
// (la fuente sigue siendo la letra/string hasta F7).
// - estado_id: cfg_estado_documento_comercial — ambiguo para pagos (A=anulado
//   colapsa a 1); para vigencia de pagos usar estado_pago_id.
// - tipo_transaccion_id: adm_tipo_transaccion (constantes TipoTransaccion).
// - estado_pago_id: adm_estado_pago (constantes EstadoPago); NULL fuera de
//   tipotransaccion 201/202.
public partial class transaccion_abonado
{
    public short? estado_id { get; set; }
    public short? tipo_transaccion_id { get; set; }
    public short? estado_pago_id { get; set; }
}
