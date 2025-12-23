using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_balance_mensual : ICompanyScopedEntity
{
    public long monthly_balance_id { get; set; }

    public long company_id { get; set; }

    public long period_id { get; set; }

    public long account_id { get; set; }

    public long? cost_center_id { get; set; }

    public short month_number { get; set; }

    public decimal debit_amount { get; set; }

    public decimal credit_amount { get; set; }

    public int transaction_count { get; set; }

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public DateTime? updated_at { get; set; }

    public virtual con_periodo_contable? period { get; set; }

    public virtual con_plan_cuenta? account { get; set; }

    public virtual con_centro_costo? cost_center { get; set; }
}
