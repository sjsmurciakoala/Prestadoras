using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Detalle mensual de depreciación por activo fijo.
/// Migrado de MySQL bdsimafi.depreinve; relacionada con af_activo_fijo por
/// codigo_activo (activo_fijo_id se resuelve tras la carga inicial).
/// </summary>
public partial class af_activo_fijo_depreciacion : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int? activo_fijo_id { get; set; }
    public string? codigo_activo { get; set; }
    public short anio { get; set; }
    public short mes { get; set; }
    public DateOnly? fecha_depreciacion { get; set; }
    public decimal valor_depreciado { get; set; }
    public decimal valor_neto_libros { get; set; }
    public string? cuenta_depreciacion { get; set; }
    public string? cuenta_gasto { get; set; }
    public string? traslado { get; set; }
    public string? descripcion { get; set; }

    public virtual af_activo_fijo? activo_fijo { get; set; }
}
