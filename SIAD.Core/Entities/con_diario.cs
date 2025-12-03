using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_diario : ICompanyScopedEntity
{
    public long journal_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = null!;

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public string? sequence_prefix { get; set; }

    public long last_sequence { get; set; }

    public bool is_active { get; set; }

    public bool allows_manual { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<con_poliza> polizas { get; set; } = new List<con_poliza>();
}
