using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_nota_cobro : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string correlativo { get; set; } = null!;
    public string codigocliente { get; set; } = null!;
    public DateOnly fecha { get; set; }
    public decimal monto { get; set; }
    public string? descripcion { get; set; }
    public string estado { get; set; } = "EMITIDA";
    public string? usuario { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
}
