using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class usuarioapc
{
    public int ide { get; set; }

    public string? nombre { get; set; }

    public string? usuario { get; set; }

    public string? clave { get; set; }

    public string? ruta { get; set; }

    public string? estado { get; set; }
}
