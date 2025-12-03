using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class vw_listaplanespago
{
    public string? correlativo { get; set; }

    public string? nombrecliente { get; set; }

    public string? estado { get; set; }

    public decimal? total { get; set; }

    public DateTime? fecha { get; set; }

    public int? idhdr { get; set; }

    public string? codcliente { get; set; }
}
