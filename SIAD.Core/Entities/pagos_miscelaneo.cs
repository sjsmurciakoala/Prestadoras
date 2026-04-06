using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class pagos_miscelaneo
{
    public long recibo { get; set; }

    public string? cliente { get; set; }

    public DateTime? fecha { get; set; }

    public decimal total { get; set; }

    public string? estado { get; set; }

    public string? banco { get; set; }

    public string? usuario { get; set; }

    public virtual ICollection<pagos_miscelaneos_dtl> pagos_miscelaneos_dtl { get; set; } = new List<pagos_miscelaneos_dtl>();
}
