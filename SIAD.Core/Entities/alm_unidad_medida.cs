using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de unidades de medida de almacén, con conversión entre unidades de
/// la misma categoría. Reemplaza el texto libre legacy de almacen.unidad.
/// </summary>
public partial class alm_unidad_medida : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string? abreviatura { get; set; }
    public bool permite_decimales { get; set; }
    public bool activo { get; set; }
    public string? categoria { get; set; }
    public int? unidad_base_id { get; set; }
    public decimal factor_conversion { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_unidad_medida? unidad_base { get; set; }
    public virtual ICollection<alm_unidad_medida> unidades_derivadas { get; set; } = new List<alm_unidad_medida>();
}
