using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class causa_refacturacion
{
    public int ide { get; set; }

    public string? codigo { get; set; }

    public string? descripcion { get; set; }

    public string? tipo { get; set; }
}
