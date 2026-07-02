using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_corte_masivo_hdr : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string correlativo { get; set; } = null!;
    public DateOnly fecha_generacion { get; set; }
    public string? criterio { get; set; }
    public int? periodo_anio { get; set; }
    public int? periodo_mes { get; set; }
    public int? ciclo_id { get; set; }
    public string? barrio_codigo { get; set; }
    public int? dias_sin_pago { get; set; }
    public decimal? valor_minimo { get; set; }
    public int? categoria_id { get; set; }
    public int dias_corte { get; set; }
    public int total_clientes { get; set; }
    public string estado { get; set; } = "GENERADO";
    public string? usuario { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
}
