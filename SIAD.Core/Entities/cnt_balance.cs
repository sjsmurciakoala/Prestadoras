using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_balance
{
    public string? cod_cuenta { get; set; }

    public int? cod_empresa { get; set; }

    public string? descripcion { get; set; }

    public Guid? rowid { get; set; }

    public int? niveles { get; set; }

    public int? cod_reporte { get; set; }
}
