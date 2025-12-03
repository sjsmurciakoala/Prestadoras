using System;
using System.Collections;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class usuarios_miorden
{
    public int id { get; set; }

    public string nombre { get; set; } = null!;

    public string usuario { get; set; } = null!;

    public string clave { get; set; } = null!;

    public int tipo { get; set; }

    public BitArray estado { get; set; } = null!;
}
