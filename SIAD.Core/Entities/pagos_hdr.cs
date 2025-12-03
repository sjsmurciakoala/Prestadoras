using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class pagos_hdr
{
    public string numfactura { get; set; } = null!;

    public string? clienteclave { get; set; }

    public DateTime? fecha { get; set; }

    public decimal total { get; set; }

    public string? estado { get; set; }

    public int? caja_id { get; set; }

    public string? banco { get; set; }

    public string? usuario { get; set; }

    public virtual catalogo_caja? caja { get; set; }

    public virtual ICollection<pagos_dtl> detalles { get; set; } = new List<pagos_dtl>();
}
