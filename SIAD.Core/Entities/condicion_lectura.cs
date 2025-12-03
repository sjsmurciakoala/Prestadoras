using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class condicion_lectura
{
    public string codigo { get; set; } = null!;

    public string? descripcion { get; set; }

    public string? tipo { get; set; }

    public string? facturacion { get; set; }
}
