using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_plantilla_partida_hdr : ICompanyScopedEntity
{
    public long template_id { get; set; }

    public long company_id { get; set; }

    public string module { get; set; } = null!;

    public string document_type { get; set; } = null!;

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public bool is_active { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<con_plantilla_partida_dtl> lineas { get; set; } = new List<con_plantilla_partida_dtl>();

    public virtual ICollection<con_partida_hdr> polizas { get; set; } = new List<con_partida_hdr>();
}


