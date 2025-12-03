using System;

namespace SIAD.Core.Entities;

public partial class orden_trabajo_estado
{
    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public bool permite_asignacion { get; set; }
}
