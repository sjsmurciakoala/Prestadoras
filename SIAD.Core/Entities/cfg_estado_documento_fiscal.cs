using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cfg_estado_documento_fiscal
{
    public short estado_id { get; set; }

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public bool activo { get; set; }
}
