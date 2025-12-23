using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_plan_cuenta : ICompanyScopedEntity
{
    public long account_id { get; set; }

    public long company_id { get; set; }

    public long? parent_account_id { get; set; }

    public string code { get; set; } = null!;

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public string account_type { get; set; } = null!;

    public string? category { get; set; }

    public short level { get; set; }

    public bool allows_posting { get; set; }

    public bool allows_budget { get; set; }

    public bool allows_third { get; set; }

    public bool is_tax_base { get; set; }

    public bool allows_cost_center { get; set; }

    public bool allows_multi_currency { get; set; }

    public string? currency_code { get; set; }

    public long? adjustment_account_id { get; set; }

    public long? correction_account_id { get; set; }

    public string status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_plan_cuenta? parent_account { get; set; }

    public virtual con_plan_cuenta? adjustment_account { get; set; }

    public virtual con_plan_cuenta? correction_account { get; set; }

    public virtual ICollection<con_plan_cuenta> child_accounts { get; set; } = new List<con_plan_cuenta>();

    public virtual ICollection<con_plantilla_poliza_linea> plantilla_lineas { get; set; } = new List<con_plantilla_poliza_linea>();

    public virtual ICollection<con_poliza_linea> poliza_lineas { get; set; } = new List<con_poliza_linea>();
}
