using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class medidor_clase
{
    public string medidor_clase_codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<maestro_medidor> maestro_medidors { get; set; } = new List<maestro_medidor>();
}
