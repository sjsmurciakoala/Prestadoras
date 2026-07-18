using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Tipo de artículo: la clasificación única de los artículos de almacén desde
/// la unificación línea→tipo (2026-07-16, seed desde alm_linea). Lleva las
/// cuentas contables del tipo y define si sus artículos manejan inventario.
/// </summary>
public partial class alm_tipo_articulo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo { get; set; } = null!;
    public string nombre { get; set; } = null!;
    public string? descripcion { get; set; }

    /// <summary>Cuentas contables (código del plan de cuentas) asociadas al tipo de artículo.</summary>
    public string? cuenta_inventario { get; set; }
    public string? cuenta_costo_ventas { get; set; }
    public string? cuenta_ventas { get; set; }
    public string? cuenta_ajustes { get; set; }
    public string? cuenta_devoluciones { get; set; }

    /// <summary>
    /// false = los artículos de este tipo NO llevan existencias (p. ej. "Servicios"):
    /// sin bodega, sin ubicación y sin kardex. La regla se aplica en la capa de servicio
    /// (ArticulosService), no en la BD.
    /// </summary>
    public bool maneja_inventario { get; set; } = true;

    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    /// <summary>Categorías (alm_grupo) que cuelgan de este tipo (unificación 2026-07-16).</summary>
    public virtual ICollection<alm_grupo> grupos { get; set; } = new List<alm_grupo>();
}
