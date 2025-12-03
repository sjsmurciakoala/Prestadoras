using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class bnc_banco
{
    public int cod_banco { get; set; }

    public string nombre { get; set; } = null!;

    public bool recibe_dinero { get; set; }

    public Guid? rowid { get; set; }

    public string? observaciones { get; set; }

    public string? codigo { get; set; }
}
