using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_mayore
{
    public int? cod_empresa { get; set; }

    public string cod_grupo_cta { get; set; } = null!;

    public string cod_sub_grupo { get; set; } = null!;

    public string cod_mayor { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public short? orden { get; set; }

    public char? partida_resumen { get; set; }

    public Guid? rowid { get; set; }
}
