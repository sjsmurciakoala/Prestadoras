using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_periodo_contable : ICompanyScopedEntity
{
    public long period_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = null!;

    public string name { get; set; } = null!;

    public DateTime start_date { get; set; }

    public DateTime end_date { get; set; }

    public string status { get; set; } = null!;

    public short? status_id { get; set; }

    public DateTime? closed_at { get; set; }

    public string? closed_by { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<con_partida_hdr> polizas { get; set; } = new List<con_partida_hdr>();
}


