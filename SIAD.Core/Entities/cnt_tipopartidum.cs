using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_tipopartidum
{
    public int cod_tipopartida { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public Guid? rowid { get; set; }
}
