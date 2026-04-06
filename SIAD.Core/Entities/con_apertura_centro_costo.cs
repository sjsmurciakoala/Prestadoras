using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_apertura_centro_costo : ICompanyScopedEntity
{
    public long opening_id { get; set; }

    public long company_id { get; set; }

    public long period_id { get; set; }

    public long account_id { get; set; }

    public long cost_center_id { get; set; }

    public short tipo_transaccion { get; set; }

    public decimal debit_amount { get; set; }

    public decimal credit_amount { get; set; }

    public string? currency_code { get; set; }

    public decimal? exchange_rate { get; set; }

    public string? notes { get; set; }

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = string.Empty;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_periodo_contable? period { get; set; }

    public virtual con_plan_cuenta? account { get; set; }

    public virtual con_centro_costo? cost_center { get; set; }
}
