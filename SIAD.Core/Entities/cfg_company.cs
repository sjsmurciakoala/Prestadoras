using System;

namespace SIAD.Core.Entities;

public partial class cfg_company
{
    public long company_id { get; set; }

    public string code { get; set; } = null!;

    public string commercial_name { get; set; } = null!;

    public string legal_name { get; set; } = null!;

    public string tax_id { get; set; } = null!;

    public string? email { get; set; }

    public string? phone { get; set; }

    public string? address { get; set; }

    public string country_code { get; set; } = null!;

    public string currency_code { get; set; } = null!;

    public string timezone { get; set; } = null!;

    public string status { get; set; } = null!;

    public string? logo_url { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
