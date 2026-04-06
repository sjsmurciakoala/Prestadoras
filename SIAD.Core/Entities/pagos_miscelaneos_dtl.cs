namespace SIAD.Core.Entities;

public partial class pagos_miscelaneos_dtl
{
    public long recibo { get; set; }

    public int linea { get; set; }

    public string? concepto { get; set; }

    public decimal? monto { get; set; }

    public virtual pagos_miscelaneo? reciboNavigation { get; set; }
}
