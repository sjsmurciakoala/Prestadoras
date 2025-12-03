using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cai
{
    public int ide { get; set; }

    public string? ruta { get; set; }

    public string? cai1 { get; set; }

    public DateOnly? fecha_emision { get; set; }

    public int? rango_inicial { get; set; }

    public int? rango_final { get; set; }

    public string? codigo_base { get; set; }

    public int? contador_actual { get; set; }
}
