using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class recolectora
{
    public string codigo { get; set; } = null!;

    public string? descripcion { get; set; }

    public string? ctabanco { get; set; }

    public decimal? aplica { get; set; }

    public string? contable { get; set; }

    public string? llave { get; set; }

    public DateOnly? vigencia { get; set; }

    public string? idbancows { get; set; }

    public byte[]? logo { get; set; }
}
