using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_llamada_cobranza : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigocliente { get; set; } = null!;
    public DateTime fecha { get; set; }
    public string? numero_llamado { get; set; }
    public string resultado { get; set; } = null!;
    public string? observacion { get; set; }
    public string? usuario { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
}
