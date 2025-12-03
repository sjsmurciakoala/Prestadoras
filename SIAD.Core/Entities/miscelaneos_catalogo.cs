using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class miscelaneos_catalogo
{
    public int ide { get; set; }

    public string? codigo { get; set; }

    public string? nombre { get; set; }

    public decimal? valor { get; set; }
}
