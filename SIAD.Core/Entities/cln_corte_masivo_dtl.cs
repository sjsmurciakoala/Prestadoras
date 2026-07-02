using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_corte_masivo_dtl : ICompanyScopedEntity
{
    public int id { get; set; }
    public int hdr_id { get; set; }
    public long company_id { get; set; }
    public string cliente_clave { get; set; } = null!;
    public string? nombre_cliente { get; set; }
    public decimal? saldo_adeudado { get; set; }
    public int? dias_sin_pago { get; set; }
    public bool pagado { get; set; }
    public DateOnly? fecha_pago { get; set; }
    public int? orden_id { get; set; }
}
