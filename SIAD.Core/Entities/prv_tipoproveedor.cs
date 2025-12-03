using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class prv_tipoproveedor
{
    public int cod_tipoproveedor { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public Guid? rowid { get; set; }
}
