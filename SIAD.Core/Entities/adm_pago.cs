using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Cabecera del cobro — el "recibo" como entidad de primera clase.
// Unificación cobranza F2 (2026-07-26), plan docs/PLAN_UNIFICACION_COBRANZA_2026-07.md §3.4.
// Folio único por empresa (numero_recibo, SIN CAI); estados en adm_estado_pago
// (constantes EstadoPago); canal en CanalCobro; idempotencia por referencia_externa.
public partial class adm_pago : ICompanyScopedEntity
{
    public long pago_id { get; set; }

    public long company_id { get; set; }

    public string numero_recibo { get; set; } = null!;

    public string cliente_clave { get; set; } = null!;

    public DateOnly fecha { get; set; }

    /// <summary>1 = caja, 2 = banco, 3 = app (constantes CanalCobro).</summary>
    public short canal_id { get; set; }

    public short tipo_transaccion_id { get; set; }

    /// <summary>adm_estado_pago: 1 aplicado, 2 pendiente, 3 anulado, 4 reversado.</summary>
    public short estado_id { get; set; }

    public decimal monto_total { get; set; }

    public string forma_pago { get; set; } = "EFECTIVO";

    public int? banco_cuenta_id { get; set; }

    public long? ban_kardex_id { get; set; }

    public int? sesion_caja_id { get; set; }

    public long? poliza_id { get; set; }

    public string? referencia_externa { get; set; }

    /// <summary>Fila legacy espejo en transaccion_abonado (dual-write F2–F7).</summary>
    public int? transaccion_abonado_ide { get; set; }

    public string? motivo_reverso { get; set; }

    public string usuario { get; set; } = null!;

    public DateTime creado_en { get; set; }

    public DateTime? actualizado_en { get; set; }

    public virtual ICollection<adm_pago_aplicacion> aplicaciones { get; set; } = new List<adm_pago_aplicacion>();
}
