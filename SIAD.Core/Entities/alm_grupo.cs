using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Grupo de producto de almacén (pertenece a una línea).
/// Migrado de MySQL bdsimafi.grupoinv.
/// </summary>
public partial class alm_grupo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string? linea_codigo { get; set; }
    public int? linea_id { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_linea? linea { get; set; }
}
