using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_poliza : ICompanyScopedEntity
{
    public long poliza_id { get; set; }

    public long company_id { get; set; }

    public long? journal_id { get; set; }

    public long? period_id { get; set; }

    public long? template_id { get; set; }

    public string module { get; set; } = null!;

    public string document_type { get; set; } = null!;

    public long? document_id { get; set; }

    public string? document_number { get; set; }

    public string poliza_number { get; set; } = null!;

    public long? sequence_number { get; set; }

    public DateTime poliza_date { get; set; }

    public string? description { get; set; }

    public string status { get; set; } = null!;

    public string? source_reference { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_diario? journal { get; set; }

    public virtual con_periodo_contable? period { get; set; }

    public virtual con_plantilla_poliza? template { get; set; }

    public virtual ICollection<con_poliza_linea> lineas { get; set; } = new List<con_poliza_linea>();
}
