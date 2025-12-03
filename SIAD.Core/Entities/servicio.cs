using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class servicio
{
    public int servicios_id { get; set; }

    public string servicios_codigo { get; set; } = null!;

    public string servicios_descripcioncorta { get; set; } = null!;

    public string? servicios_descripcionlarga { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }
}
