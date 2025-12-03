using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_sub_cuentum
{
    public int? cod_empresa { get; set; }

    public string cod_grupo { get; set; } = null!;

    public string csgrupo { get; set; } = null!;

    public string cod_mayor { get; set; } = null!;

    public string cscuenta { get; set; } = null!;

    public string? descripcion { get; set; }

    public Guid? rowid { get; set; }
}
