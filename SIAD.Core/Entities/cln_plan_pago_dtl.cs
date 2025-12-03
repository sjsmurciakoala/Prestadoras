using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cln_plan_pago_dtl
{
    public int id { get; set; }

    public int? idhdr { get; set; }

    public decimal? valorcuota { get; set; }

    public DateTime? fechacuota { get; set; }

    public int? mes { get; set; }

    public string? estadopago { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual cln_plan_pago_hdr? idhdrNavigation { get; set; }
}
