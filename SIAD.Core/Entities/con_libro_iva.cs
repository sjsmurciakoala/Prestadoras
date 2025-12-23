using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_libro_iva : ICompanyScopedEntity
{
    public long iva_register_id { get; set; }

    public long company_id { get; set; }

    public long period_id { get; set; }

    public DateTime transaction_date { get; set; }

    public string document_type { get; set; } = string.Empty;

    public string document_number { get; set; } = string.Empty;

    public long? third_party_id { get; set; }

    public decimal taxable_base { get; set; }

    public decimal exempt_amount { get; set; }

    public decimal tax_rate { get; set; }

    public decimal tax_amount { get; set; }

    public decimal total_amount { get; set; }

    public string iva_type { get; set; } = string.Empty;

    public string status { get; set; } = "RECORDED";

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = string.Empty;

    public virtual con_periodo_contable? period { get; set; }

    public virtual con_tercero? third_party { get; set; }
}
