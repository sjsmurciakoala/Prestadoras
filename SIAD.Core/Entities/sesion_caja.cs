using System;

namespace SIAD.Core.Entities;

public partial class sesion_caja
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string usuario_apertura { get; set; } = null!;
    public DateTime fecha_apertura { get; set; }
    public string? usuario_cierre { get; set; }
    public DateTime? fecha_cierre { get; set; }
    public string estado { get; set; } = "ABIERTA";
    public decimal? total_cobrado { get; set; }
    public string? observacion { get; set; }
}
