using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class axl_observacion_cobranza
{
    public int id { get; set; }

    public string observacion { get; set; } = null!;

    public bool activo { get; set; }

    public Guid? rowid { get; set; }
}
