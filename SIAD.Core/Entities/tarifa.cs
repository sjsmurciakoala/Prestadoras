using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class tarifa
{
    public int tipo { get; set; }

    public int categoria_id { get; set; }

    public string codigo { get; set; } = null!;

    public string? descripcion { get; set; }

    public decimal? valor { get; set; }
}
