using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cln_accion_cobranza
{
    public int id { get; set; }

    public string codigocliente { get; set; } = null!;

    public DateTime fecha { get; set; }

    public string accion { get; set; } = null!;

    public string? observacion { get; set; }
}
