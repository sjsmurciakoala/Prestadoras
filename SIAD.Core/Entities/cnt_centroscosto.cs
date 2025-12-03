using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_centroscosto
{
    public int cod_centrocosto { get; set; }

    public string nom_centrocosto { get; set; } = null!;

    public char? status { get; set; }

    public bool? flag_tipo_cc { get; set; }

    public DateOnly? fechadesde { get; set; }

    public DateOnly? fechahasta { get; set; }

    public Guid? rowid { get; set; }
}
