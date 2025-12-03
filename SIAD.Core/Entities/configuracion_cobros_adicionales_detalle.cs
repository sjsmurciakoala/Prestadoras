using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class configuracion_cobros_adicionales_detalle
{
    public int ide { get; set; }

    public int configuracion_cobro_adicional_ide { get; set; }

    public int servicio_id { get; set; }

    public decimal? porcentaje { get; set; }

    public virtual configuracion_cobros_adicionale configuracion_cobro_adicional_ideNavigation { get; set; } = null!;
}
