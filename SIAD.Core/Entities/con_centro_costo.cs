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

    public int? legacy_key_cost { get; set; }

    public short legacy_type_trans { get; set; }

    public string? legacy_parent_code { get; set; }

    public bool allows_movement { get; set; }

    public DateTime? start_date { get; set; }

    public DateTime? end_date { get; set; }

    public string? legacy_notes { get; set; }

    public bool is_periodic { get; set; }

    public bool legacy_status { get; set; }

    public virtual ICollection<con_apertura_centro_costo> aperturas_centro_costo { get; set; }
        = new List<con_apertura_centro_costo>();

    public virtual ICollection<con_plantilla_partida_dtl> plantilla_lineas { get; set; } = new List<con_plantilla_partida_dtl>();

    public virtual ICollection<con_partida_dtl> poliza_lineas { get; set; } = new List<con_partida_dtl>();
}


