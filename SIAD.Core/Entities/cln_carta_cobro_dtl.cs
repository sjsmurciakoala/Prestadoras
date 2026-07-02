using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_carta_cobro_dtl : ICompanyScopedEntity
{
    public int id { get; set; }
    public int hdr_id { get; set; }
    public long company_id { get; set; }
    public string cliente_clave { get; set; } = null!;
    public string? nombre_cliente { get; set; }
    public decimal? saldo { get; set; }
    public int? dias_mora { get; set; }
}
