using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_activo_tipo : ICompanyScopedEntity
{
    public long type_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = string.Empty;

    public string name { get; set; } = string.Empty;

    public string? description { get; set; }

    public string depreciation_method { get; set; } = "STRAIGHT_LINE";

    public short useful_life_years { get; set; }

    public string status { get; set; } = "ACTIVE";

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = string.Empty;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<con_activo_fijo> activos { get; set; } = new List<con_activo_fijo>();
}
