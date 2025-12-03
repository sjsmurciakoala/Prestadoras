using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_centro_costos_subgrupo
{
    public string codccg { get; set; } = null!;

    public string codsccg { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public Guid? rowid { get; set; }
}
