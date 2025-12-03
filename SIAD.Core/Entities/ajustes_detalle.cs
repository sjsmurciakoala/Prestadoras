using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ajustes_detalle
{
    public int ide { get; set; }

    public int? documento { get; set; }

    public string? tipo_servicio { get; set; }

    public decimal? monto { get; set; }
}
