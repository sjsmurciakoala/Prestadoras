using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Catálogo de artículos de almacén con existencia actual.
/// Migrado de MySQL bdsimafi.almacen (PK original codproduc).
/// </summary>
public partial class alm_articulo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo_articulo { get; set; } = null!;
    public string descripcion { get; set; } = null!;
    public DateOnly? fecha_registro { get; set; }
    public decimal cantidad { get; set; }
    public decimal existencia { get; set; }
    public decimal existencia_minima { get; set; }
    public decimal valor_unitario { get; set; }
    public string? linea { get; set; }
    public string? grupo { get; set; }
    public string? unidad_medida { get; set; }
    public string? diametro { get; set; }
    public string? cuenta_contable { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    /// <summary>FK opcional al catálogo alm_unidad_medida (convive con el texto libre unidad_medida).</summary>
    public int? unidad_medida_id { get; set; }
    public virtual alm_unidad_medida? unidad_medida_ref { get; set; }

    /// <summary>FK opcional a la unidad en que se almacena el artículo (catálogo alm_unidad_medida).</summary>
    public int? unidad_almacenaje_id { get; set; }
    public virtual alm_unidad_medida? unidad_almacenaje_ref { get; set; }

    /// <summary>FK opcional a la unidad en que se despacha/consume el artículo (catálogo alm_unidad_medida).</summary>
    public int? unidad_salida_id { get; set; }
    public virtual alm_unidad_medida? unidad_salida_ref { get; set; }

    /// <summary>FK opcional a la clasificación por uso (operativo/mantenimiento/consumo).</summary>
    public int? tipo_articulo_id { get; set; }
    public virtual alm_tipo_articulo? tipo_articulo_ref { get; set; }

    /// <summary>FK opcional a la línea de inventario (convive con el código legacy linea).</summary>
    public int? linea_id { get; set; }
    public virtual alm_linea? linea_ref { get; set; }

    /// <summary>FK opcional al grupo de producto (convive con el código legacy grupo).</summary>
    public int? grupo_id { get; set; }
    public virtual alm_grupo? grupo_ref { get; set; }

    /// <summary>Ubicaciones del artículo por bodega (bodega + estante + principal). Una fila por bodega.</summary>
    public virtual ICollection<alm_articulo_bodega> ubicaciones { get; set; } = new List<alm_articulo_bodega>();

    /// <summary>Proveedores que suministran el artículo ("UPC"). Una fila por proveedor.</summary>
    public virtual ICollection<alm_articulo_proveedor> proveedores { get; set; } = new List<alm_articulo_proveedor>();
}
