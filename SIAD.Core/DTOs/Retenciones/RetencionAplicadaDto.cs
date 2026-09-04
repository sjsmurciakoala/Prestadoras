namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Retención efectivamente aplicada en un pago (procesar/abono), transportada en paralelo a las
/// líneas de la partida (F4). Sirve para escribir el registro fiscal prv_retencion_hdr/dtl SIN
/// desincronizarlo con la partida: <see cref="CuentaId"/> es la misma cuenta del pasivo que la
/// línea al Haber, y el servicio valida que Σ <see cref="Monto"/> == Σ crédito de esas cuentas.
/// </summary>
public sealed class RetencionAplicadaDto
{
    /// <summary>Concepto de retención del catálogo (cfg_retencion.id).</summary>
    public int RetencionId { get; set; }

    /// <summary>Cuenta contable del pasivo (con_plan_cuentas.account_id) — == la de la línea POSTED.</summary>
    public long CuentaId { get; set; }

    /// <summary>Base imponible de ESTA retención (TOTAL = bruto; SIN_ISV = bruto / (1 + isv)).</summary>
    public decimal Base { get; set; }

    /// <summary>Porcentaje aplicado (snapshot de la tasa vigente a la fecha del pago).</summary>
    public decimal Porcentaje { get; set; }

    /// <summary>Monto retenido = round(Base * Porcentaje/100, 2).</summary>
    public decimal Monto { get; set; }
}
