using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Kardex de movimientos de bodega (ingresos/salidas por artículo).
/// Migrado de MySQL bdsimafi.inventariotra; se relaciona con alm_articulo
/// por codigo_articulo (no con af_activo_fijo pese al nombre de origen).
/// </summary>
public partial class alm_kardex : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public decimal? numero_documento { get; set; }
    public string? tipo_transaccion { get; set; }
    public DateOnly? fecha { get; set; }
    public string? codigo_articulo { get; set; }
    public decimal cantidad { get; set; }
    public string? bodega { get; set; }
    public int? bodega_id { get; set; }
    public decimal ingresos { get; set; }
    public decimal salidas { get; set; }
    public decimal valor_unitario { get; set; }
    public decimal total { get; set; }
    public decimal debe { get; set; }
    public decimal haber { get; set; }
    public string? cuenta_contable { get; set; }
    public string? departamento { get; set; }
    public string? departamento_desc { get; set; }
    public string? linea { get; set; }
    public string? linea_desc { get; set; }
    public string? barrio { get; set; }
    public bool es_ajuste { get; set; }
    public string? descripcion { get; set; }
    public string? observacion { get; set; }

    /// <summary>
    /// Bodega normalizada (FK a alm_bodega). La columna legacy <see cref="bodega"/>
    /// (VARCHAR de texto libre migrado) se conserva; ésta es la referencia real al catálogo.
    /// </summary>
    public virtual alm_bodega? bodega_ref { get; set; }
}
