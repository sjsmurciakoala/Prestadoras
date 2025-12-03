using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class con_centro_costo : ICompanyScopedEntity
{
    public long cost_center_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = null!;

    public string name { get; set; } = null!;

    public string? description { get; set; }

    public string status { get; set; } = null!;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<con_plantilla_poliza_linea> plantilla_lineas { get; set; } = new List<con_plantilla_poliza_linea>();

    public virtual ICollection<con_poliza_linea> poliza_lineas { get; set; } = new List<con_poliza_linea>();
}
