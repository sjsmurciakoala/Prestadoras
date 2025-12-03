using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class view_get_compromisos_hdr
{
    public int? numero_orden { get; set; }

    public string? proveedor { get; set; }

    public string? rtn { get; set; }

    public string? concepto { get; set; }

    public decimal? monto { get; set; }

    public string? cuenta_contable { get; set; }

    public string? cod_proveedor { get; set; }
}
