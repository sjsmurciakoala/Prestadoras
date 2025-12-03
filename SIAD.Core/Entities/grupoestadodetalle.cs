using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class grupoestadodetalle
{
    public int idgrupodetalle { get; set; }

    public string nombre { get; set; } = null!;

    public int idgrupo { get; set; }

    public virtual grupoestado idgrupoNavigation { get; set; } = null!;
}
