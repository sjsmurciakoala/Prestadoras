using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class configuracion_tasas_detalle
{
    public int configuracion_tasas_detalle_id { get; set; }

    public int configuracion_tasas_id { get; set; }

    public int servicios_id { get; set; }

    public bool configuracion_tasas_detalle_aplicaservicio { get; set; }

    public decimal configuracion_tasas_detalle_monto { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual configuracion_tasa configuracion_tasas { get; set; } = null!;
}
