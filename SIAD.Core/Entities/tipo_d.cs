using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class tipo_d
{
    public int tipo_id { get; set; }

    public string? depto { get; set; }

    public string? tipo { get; set; }

    public string? descripcion { get; set; }

    public string? concepto { get; set; }

    public string? depto_appmitrabajo { get; set; }
}
