using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_centrocostos_hdr
{
    public string cuenta { get; set; } = null!;

    public string codccg { get; set; } = null!;

    public string codsccg { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? contable { get; set; }
}
