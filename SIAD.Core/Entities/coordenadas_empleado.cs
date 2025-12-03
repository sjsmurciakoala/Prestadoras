using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class coordenadas_empleado
{
    public int id { get; set; }

    public string? nombre { get; set; }

    public int? ano { get; set; }

    public int? mes { get; set; }

    public int? dia { get; set; }

    public DateTime? fecha { get; set; }

    public string? latitud { get; set; }

    public string? longitud { get; set; }
}
