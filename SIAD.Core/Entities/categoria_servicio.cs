using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class categoria_servicio
{
    public int categoria_servicio_id { get; set; }

    public string descripcion { get; set; } = null!;

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<cliente_maestro> cliente_maestros { get; set; } = new List<cliente_maestro>();

    public virtual ICollection<solicitud_servicio> solicitud_servicios { get; set; } = new List<solicitud_servicio>();
}
