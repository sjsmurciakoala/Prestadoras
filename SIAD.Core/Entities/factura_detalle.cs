using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class factura_detalle
{
    public int id { get; set; }

    public int? numrecibo { get; set; }

    public string? codigo { get; set; }

    public string? tiposervicio { get; set; }

    public string? descripcion { get; set; }

    public decimal? montovalor { get; set; }

    public int? factura_id { get; set; }

    public decimal? montovalor_saldo { get; set; }
}
