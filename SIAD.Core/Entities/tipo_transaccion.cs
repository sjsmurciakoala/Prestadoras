using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class tipo_transaccion
{
    public int ide { get; set; }

    public string? codigo { get; set; }

    public string? descripcion { get; set; }

    public bool? estado { get; set; }

    public string? usuario_actualizacion { get; set; }

    public string? fecha_actualizacion { get; set; }
}
