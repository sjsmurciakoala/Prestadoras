using System;

namespace SIAD.Core.Entities;

public partial class pagos_miscelaneo
{
    public long recibo { get; set; }

    public string? cliente { get; set; }

    public DateTime? fecha { get; set; }

    public decimal total { get; set; }

    public string? estado { get; set; }
}
