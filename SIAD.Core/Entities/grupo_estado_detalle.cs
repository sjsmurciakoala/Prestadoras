using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class grupo_estado_detalle
{
    public int ide { get; set; }

    public string nombre { get; set; } = null!;

    public int? grupo_id { get; set; }
}
