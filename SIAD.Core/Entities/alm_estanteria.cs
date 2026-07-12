using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class alm_estanteria : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int bodega_id { get; set; }
    public string codigo { get; set; } = null!;
    public string? nombre { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_bodega? bodega { get; set; }
    public virtual ICollection<alm_estante> estantes { get; set; } = new List<alm_estante>();
}
