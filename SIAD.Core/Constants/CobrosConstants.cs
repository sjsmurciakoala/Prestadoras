namespace SIAD.Core.Constants;

// Unificación cobranza F2 (2026-07-26) — docs/PLAN_UNIFICACION_COBRANZA_2026-07.md.

/// <summary>Canal por el que entra un cobro (adm_pago.canal_id).</summary>
public static class CanalCobro
{
    public const short Caja  = 1;   // ventanilla — exige sesión de caja abierta
    public const short Banco = 2;   // WS bancario (F5)
    public const short App   = 3;   // futuro: app / kioscos
}

/// <summary>Tipo de documento al que se aplica un cobro (adm_pago_aplicacion.documento_tipo).</summary>
public static class DocumentoCobroTipo
{
    public const short Factura    = 1;
    public const short CuotaPlan  = 2;   // F6
    public const short NotaDebito = 3;   // futuro
}

/// <summary>Series de folios de adm_documento_secuencia.</summary>
public static class TipoDocumentoSecuencia
{
    public const string ReciboPago = "RECIBO_PAGO";
}
