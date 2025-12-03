using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class tipo_uso_servicio
{
    public string tipo_uso_codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<cliente_maestro> cliente_maestros { get; set; } = new List<cliente_maestro>();
}
