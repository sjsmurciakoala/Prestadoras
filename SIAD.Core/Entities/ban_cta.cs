using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_cta
{
    public long ban_cta_id { get; set; }

    public long company_id { get; set; }

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public int iea { get; set; }

    public int ecg { get; set; }

    public string? grupo { get; set; }

    public DateTime? u_fecha { get; set; }

    public string? u_dcto { get; set; }

    public string? u_banco { get; set; }

    public string? u_benef { get; set; }

    public string? u_coment1 { get; set; }

    public string? u_coment2 { get; set; }

    public decimal u_monto { get; set; }

    public bool es_banco { get; set; }

    public int tdc { get; set; }

    public decimal saldo_actual { get; set; }

    public string? tercero { get; set; }

    public string? cod_centro { get; set; }

    public int cta_cf { get; set; }

    public int cta_mov { get; set; }

    public int cta_ter { get; set; }

    public int cta_cc { get; set; }

    public int cta_base { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<ban_cuenta> ban_cuenta { get; set; } = new List<ban_cuenta>();

    public virtual cfg_company company { get; set; } = null!;
}
