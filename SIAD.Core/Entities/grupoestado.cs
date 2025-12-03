using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class grupoestado
{
    public int idgrupo { get; set; }

    public string nombre { get; set; } = null!;

    public virtual ICollection<grupoestadodetalle> grupoestadodetalles { get; set; } = new List<grupoestadodetalle>();
}
