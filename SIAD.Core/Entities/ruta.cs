using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ruta
{
    public int id { get; set; }

    public int? codciclo { get; set; }

    public string? codruta { get; set; }

    public string? descripcion { get; set; }

    public virtual ciclo? codcicloNavigation { get; set; }
}
