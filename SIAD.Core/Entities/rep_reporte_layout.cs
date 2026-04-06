using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class rep_reporte_layout : ICompanyScopedEntity
{
    public long report_layout_id { get; set; }

    public long company_id { get; set; }

    public long informe_id { get; set; }

    public int version_num { get; set; }

    public string estado { get; set; } = null!;

    public string layout_xml { get; set; } = null!;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public DateTime? published_at { get; set; }

    public string? published_by { get; set; }
}
