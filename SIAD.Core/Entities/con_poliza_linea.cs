using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_poliza_linea : ICompanyScopedEntity
{
    public long poliza_line_id { get; set; }

    public long company_id { get; set; }

    public long poliza_id { get; set; }

    public short line_number { get; set; }

    public long account_id { get; set; }

    public long? cost_center_id { get; set; }

    public string? description { get; set; }

    public decimal debit_amount { get; set; }

    public decimal credit_amount { get; set; }

    public string? currency_code { get; set; }

    public decimal? exchange_rate { get; set; }

    public string? source_document { get; set; }

    public virtual con_plan_cuenta? account { get; set; }

    public virtual con_centro_costo? cost_center { get; set; }

    public virtual con_poliza? poliza { get; set; }
}
