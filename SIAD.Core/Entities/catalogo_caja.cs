using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class catalogo_caja
{
    public int id { get; set; }

    public string? nombre { get; set; }

    public string? estado { get; set; }

    public DateTime? fecha_apertura { get; set; }

    public string? usuario { get; set; }

    public virtual ICollection<pagos_hdr> pagos_hdrs { get; set; } = new List<pagos_hdr>();
}
