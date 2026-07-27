using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Unificación cobranza F6 (2026-07-29): traslado de deuda al crear un plan de
/// pago — registro de qué líneas de factura se compensaron (derrame FIFO por
/// montoFinanciar) y por cuánto. Contraparte documental del viejo movimiento
/// legacy 'PLAN' (crédito); permite anular el plan restituyendo saldos exactos.
/// </summary>
public partial class cln_plan_pago_traslado : ICompanyScopedEntity
{
    public long traslado_id { get; set; }

    public long company_id { get; set; }

    public int plan_id { get; set; }

    public int factura_id { get; set; }

    public int factura_detalle_id { get; set; }

    public decimal monto_trasladado { get; set; }

    public DateTime creado_en { get; set; }

    public string creado_por { get; set; } = null!;
}
