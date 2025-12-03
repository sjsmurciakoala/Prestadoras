using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class log_cliclo_descarga_app
{
    public int ide { get; set; }

    public int? anio { get; set; }

    public int? mes { get; set; }

    public int? ciclo { get; set; }

    public string? usuario { get; set; }

    public DateTime? fecha { get; set; }
}
