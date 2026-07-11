using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Rubro/línea de inventario de almacén. Migrado de MySQL bdsimafi.lineas.
/// </summary>
public partial class alm_linea : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string? cuenta_contable { get; set; }
    public string? cuenta_contable_anterior { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<alm_grupo> grupos { get; set; } = new List<alm_grupo>();
}
