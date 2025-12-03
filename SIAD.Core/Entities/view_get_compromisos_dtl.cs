using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class view_get_compromisos_dtl
{
    public int? numero_orden_dtl { get; set; }

    public string? cod_presupuestario { get; set; }

    public string? actividad { get; set; }

    public string? programa { get; set; }

    public string? objeto_gasto { get; set; }

    public string? contable { get; set; }

    public decimal? montodtl { get; set; }

    public string? descripciondtl { get; set; }
}
