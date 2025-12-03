using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class usuarios_tipotransaccion_hdr
{
    public int id_usertransacc { get; set; }

    public string usuario { get; set; } = null!;

    public DateTime fecha_creacion { get; set; }

    public string usuario_creo { get; set; } = null!;
}
