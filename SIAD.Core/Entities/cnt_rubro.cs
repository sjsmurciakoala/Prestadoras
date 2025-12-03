using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_rubro
{
    public int cod_empresa { get; set; }

    public int cod_reporte { get; set; }

    public string nombre { get; set; } = null!;

    public int orden_reporte { get; set; }

    public Guid? rowid { get; set; }
}
