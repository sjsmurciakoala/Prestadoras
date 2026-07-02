using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class cln_cliente_estado_log : ICompanyScopedEntity
{
    public long id { get; set; }
    public long company_id { get; set; }
    public string codigocliente { get; set; } = null!;
    public string tipo { get; set; } = null!;
    public bool? valor_anterior { get; set; }
    public bool valor_nuevo { get; set; }
    public string? motivo { get; set; }
    public string usuario { get; set; } = null!;
    public DateTime fecha { get; set; }
}
