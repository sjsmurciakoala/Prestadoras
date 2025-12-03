using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class axl_accion_cobranza
{
    public int cod_accion { get; set; }

    public string nombre { get; set; } = null!;

    public Guid? rowid { get; set; }
}
