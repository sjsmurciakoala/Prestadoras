using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Unificación cobranza F7 H1 (2026-07-30): recibo "para pagar en banco"
/// emitido en ventanilla — antes era una fila 202/'P' de transaccion_abonado.
/// NO rebaja la factura; se aplica al cobrarse en caja (cobrado_pago_id, el
/// documento del motor) o se anula (manual o cubierto por la conciliación
/// automática al saldarse la factura). estado_id reusa adm_estado_pago
/// (2 PENDIENTE, 1 APLICADO, 3 ANULADO).
/// </summary>
public partial class adm_recibo_banco_pendiente : ICompanyScopedEntity
{
    public long recibo_pendiente_id { get; set; }

    public long company_id { get; set; }

    public string cliente_clave { get; set; } = null!;

    public int factura_id { get; set; }

    public int numrecibo { get; set; }

    public decimal monto { get; set; }

    public short estado_id { get; set; } = 2;

    public string? descripcion { get; set; }

    public string generado_por { get; set; } = null!;

    public DateTime generado_en { get; set; }

    public long? cobrado_pago_id { get; set; }

    public string? anulado_por { get; set; }

    public DateTime? anulado_en { get; set; }

    public string? motivo_anulacion { get; set; }

    /// <summary>Fila legacy de origen (migración F7; NULL para recibos nuevos).</summary>
    public int? transaccion_abonado_ide { get; set; }
}
