using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class tarifas_contador
{
    public int ide { get; set; }

    public int tipo { get; set; }

    public int? categoria_id { get; set; }

    public string? codigo { get; set; }

    public string? descripcion { get; set; }

    public decimal? minimo { get; set; }

    public decimal? maximo { get; set; }

    public decimal? cuota { get; set; }

    public decimal? valor_base { get; set; }

    public decimal? alquiler { get; set; }
}
