using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Saldos de cuentas contables por período, mes y tipo de transacción
/// Equivalente a C01AcctBalance en sistema de referencia
/// </summary>
public class con_saldo_cuenta : ICompanyScopedEntity
{
    public long saldo_id { get; set; }

    public long company_id { get; set; }

    public long periodo_id { get; set; }

    public string codigo_cuenta { get; set; } = null!;

    public byte mes { get; set; }  // 1-13 (13 = Acumulado)

    public byte tipo_transaccion { get; set; }  // 0-5

    public decimal debitos { get; set; }

    public decimal creditos { get; set; }

    public int cantidad_debitos { get; set; }

    public int cantidad_creditos { get; set; }

    public decimal presupuesto { get; set; }

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public DateTime? updated_at { get; set; }

    // Navigation
    public virtual con_periodo_contable? periodo { get; set; }

    public virtual con_plan_cuenta? cuenta { get; set; }
}
