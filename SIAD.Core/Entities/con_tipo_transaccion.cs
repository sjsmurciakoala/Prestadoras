using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_tipo_transaccion : ICompanyScopedEntity
{
    public long type_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = string.Empty;

    public string name { get; set; } = string.Empty;

    public string? description { get; set; }

    public string category { get; set; } = string.Empty;

    public bool is_automatic { get; set; }

    public bool allows_cost_center { get; set; }

    public bool allows_third_party { get; set; }

    public string status { get; set; } = "ACTIVE";

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = string.Empty;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
