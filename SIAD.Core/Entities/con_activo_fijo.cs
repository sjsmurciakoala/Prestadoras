using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_activo_fijo : ICompanyScopedEntity
{
    public long asset_id { get; set; }

    public long company_id { get; set; }

    public long asset_type_id { get; set; }

    public string code { get; set; } = string.Empty;

    public string name { get; set; } = string.Empty;

    public string? description { get; set; }

    public DateTime acquisition_date { get; set; }

    public DateTime in_service_date { get; set; }

    public decimal acquisition_cost { get; set; }

    public decimal salvage_value { get; set; }

    public short useful_life_years { get; set; }

    public string depreciation_method { get; set; } = "STRAIGHT_LINE";

    public decimal accumulated_depreciation { get; set; }

    public decimal? current_value { get; set; }

    public long? asset_account_id { get; set; }

    public long? depreciation_account_id { get; set; }

    public string? location { get; set; }

    public string status { get; set; } = "ACTIVE";

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = string.Empty;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_activo_tipo? asset_type { get; set; }

    public virtual con_plan_cuenta? asset_account { get; set; }

    public virtual con_plan_cuenta? depreciation_account { get; set; }
}
