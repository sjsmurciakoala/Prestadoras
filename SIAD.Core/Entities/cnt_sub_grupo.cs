using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_sub_grupo
{
    public int? cod_empresa { get; set; }

    public string cod_grupo { get; set; } = null!;

    public string cod_sub_grupo { get; set; } = null!;

    public string? descripcion { get; set; }

    public Guid? rowid { get; set; }
}
