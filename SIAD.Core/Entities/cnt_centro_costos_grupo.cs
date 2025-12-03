using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_centro_costos_grupo
{
    public int? cod_empresa { get; set; }

    public string codccg { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public Guid? rowid { get; set; }
}
