using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class prv_compromiso_hdr
{
    public int numero_orden { get; set; }

    public int? correlativo_proveedor { get; set; }

    public DateTime fecha { get; set; }

    public decimal monto { get; set; }

    public string concepto { get; set; } = null!;

    public string? cod_proveedor { get; set; }

    public int? flag_proveedor { get; set; }

    public string? cuenta_contable { get; set; }

    public string? cod_proyecto { get; set; }

    public string? rtn { get; set; }

    public string? pagar_a { get; set; }

    public bool? status_transacc { get; set; }

    public string? nombre_proveedor { get; set; }

    public bool anulado { get; set; }
}
