using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class configuracion_tasa
{
    public int configuracion_tasas_id { get; set; }

    public int maestro_cliente_id { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<configuracion_tasas_detalle> configuracion_tasas_detalles { get; set; } = new List<configuracion_tasas_detalle>();

    public virtual cliente_maestro maestro_cliente { get; set; } = null!;
}
