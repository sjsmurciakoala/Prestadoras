using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_centrocostos_dtl
{
    public string cuenta { get; set; } = null!;

    public decimal aprobado { get; set; }

    public decimal compro { get; set; }

    public decimal pagado { get; set; }

    public decimal obs { get; set; }

    public decimal valor { get; set; }

    public decimal ampl { get; set; }

    public decimal saldo { get; set; }

    public decimal mov { get; set; }

    public decimal transfe { get; set; }

    public decimal fondo { get; set; }

    public decimal proyeccion { get; set; }

    public decimal nuevoaprobado { get; set; }

    public string tipo { get; set; } = null!;
}
