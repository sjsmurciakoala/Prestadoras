using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_regla_integracion : ICompanyScopedEntity
{
    public long regla_id { get; set; }

    public long company_id { get; set; }

    public string module { get; set; } = null!;

    public long document_type_id { get; set; }

    public string scenario_code { get; set; } = null!;

    public string? description { get; set; }

    public long debit_account_id { get; set; }

    public long credit_account_id { get; set; }

    public long? cost_center_id { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_plan_cuenta? debit_account { get; set; }

    public virtual con_plan_cuenta? credit_account { get; set; }

    public virtual con_centro_costo? cost_center { get; set; }
}
