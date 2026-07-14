using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de categorías (tipos) de unidades de medida: Peso, Volumen, Longitud,
/// Cantidad, etc. Las unidades de una misma categoría son convertibles entre sí.
/// </summary>
public partial class alm_categoria_unidad : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string nombre { get; set; } = null!;
    public string? descripcion { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<alm_unidad_medida> unidades { get; set; } = new List<alm_unidad_medida>();
}
