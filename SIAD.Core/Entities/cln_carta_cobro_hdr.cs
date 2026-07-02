using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_carta_cobro_hdr : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string correlativo { get; set; } = null!;
    public DateOnly fecha_generacion { get; set; }
    public int total_clientes { get; set; }
    public string? criterio { get; set; }
    public string? usuario { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public int? plazo_horas { get; set; }
}
