namespace SIAD.Core.Entities;

// Espejo numérico de factura.estado (unificación cobranza F1, 2026-07).
// Lo mantiene el trigger trg_factura_sync_estado_id en BD; catálogo
// cfg_estado_documento_comercial (constantes EstadoDocumentoComercial,
// incluye 4 = Parcialmente abonada 'B').
public partial class factura
{
    public short? estado_id { get; set; }
}
