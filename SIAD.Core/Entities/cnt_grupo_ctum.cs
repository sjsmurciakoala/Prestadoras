using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_grupo_ctum
{
    public int? cod_empresa { get; set; }

    public string cod_grupo_cta { get; set; } = null!;

    public string? nombre { get; set; }

    public Guid? rowid { get; set; }
}
