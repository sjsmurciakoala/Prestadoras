using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cfg_document_type : ICompanyScopedEntity
{
    public long document_type_id { get; set; }

    public long company_id { get; set; }

    public string module { get; set; } = null!;

    public string code { get; set; } = null!;

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public bool requires_cai { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
