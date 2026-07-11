using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class alm_estante : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int estanteria_id { get; set; }
    public string codigo { get; set; } = null!;
    public string? descripcion { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_estanteria? estanteria { get; set; }
}
