using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_plantilla_poliza_linea : ICompanyScopedEntity
{
    public long template_line_id { get; set; }

    public long company_id { get; set; }

    public long template_id { get; set; }

    public short line_number { get; set; }

    public long account_id { get; set; }

    public long? cost_center_id { get; set; }

    public string? debit_formula { get; set; }

    public string? credit_formula { get; set; }

    public string? description { get; set; }

    public virtual con_plan_cuenta? account { get; set; }

    public virtual con_centro_costo? cost_center { get; set; }

    public virtual con_plantilla_poliza? template { get; set; }
}
