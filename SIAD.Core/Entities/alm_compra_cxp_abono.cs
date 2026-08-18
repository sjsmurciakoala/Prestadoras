using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Abono / pago de una cuenta por pagar de compra (<see cref="alm_compra_cxp"/>). Patrón de
/// <c>prv_compromiso_abono</c>: <c>numero_abono</c> corrido por CxP, estado 'V' vigente / 'A'
/// anulado. Guarda el movimiento bancario y —cuando aplique— la partida contable del pago.
/// </summary>
public partial class alm_compra_cxp_abono : ICompanyScopedEntity
{
    public long id { get; set; }
    public long company_id { get; set; }

    public int cxp_id { get; set; }

    /// <summary>Correlativo del abono dentro de la CxP (1, 2, 3…).</summary>
    public int numero_abono { get; set; }

    public DateOnly fecha { get; set; }

    /// <summary>Bruto aplicado a la deuda (baja el saldo de la CxP por este monto).</summary>
    public decimal monto { get; set; }

    /// <summary>Suma retenida en este pago (0 si no hubo). El neto pagado al banco/caja = monto − retenido.</summary>
    public decimal retenido { get; set; }

    /// <summary>efectivo / cheque / transferencia.</summary>
    public string? metodo_pago { get; set; }

    public int? banco_cuenta_id { get; set; }
    public string? num_cheque { get; set; }

    /// <summary>Movimiento bancario generado por el pago.</summary>
    public long? ban_kardex_id { get; set; }

    /// <summary>Partida contable del pago (cuando la integración contable esté activa).</summary>
    public long? partida_id { get; set; }
    public long? partida_reverso_id { get; set; }

    /// <summary>Kardex bancario de la anulación del pago (reversa).</summary>
    public long? ban_kardex_id_reverso { get; set; }

    /// <summary>'V' vigente · 'A' anulado.</summary>
    public string estado { get; set; } = "V";

    public string? motivo_anulacion { get; set; }
    public string? observaciones { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuarioanulacion { get; set; }
    public DateTime? fechaanulacion { get; set; }

    public virtual alm_compra_cxp? cxp { get; set; }
}
