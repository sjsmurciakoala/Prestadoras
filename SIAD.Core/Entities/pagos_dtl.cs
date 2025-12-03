namespace SIAD.Core.Entities;

public partial class pagos_dtl
{
    public string numfactura { get; set; } = null!;

    public int linea { get; set; }

    public string? servicio { get; set; }

    public decimal? monto { get; set; }

    public decimal? montovalor { get; set; }

    public virtual pagos_hdr? pago { get; set; }
}
