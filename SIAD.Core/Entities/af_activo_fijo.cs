using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Maestro de activo fijo con datos de depreciación.
/// Migrado de MySQL bdsimafi.inventario (nombre de origen engañoso: es activo
/// fijo, no existencias de bodega).
/// </summary>
public partial class af_activo_fijo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public string codigo_activo { get; set; } = null!;
    public string descripcion { get; set; } = null!;
    public string? tipo { get; set; }
    public string? clase { get; set; }
    public string? modelo { get; set; }
    public string? serie { get; set; }
    public string? propiedades_especiales { get; set; }
    public short? estado { get; set; }
    public string? ubicacion { get; set; }
    public string? direccion_foto { get; set; }
    public string? codigo_empleado { get; set; }
    public string? responsable { get; set; }
    public string? cargo_responsable { get; set; }
    public string? origen { get; set; }
    public string? origen_desc { get; set; }
    public string? proveedor { get; set; }
    public string? numero_factura { get; set; }
    public decimal? numero_cheque { get; set; }
    public DateOnly? fecha_compra { get; set; }
    public DateOnly? fecha_cheque { get; set; }
    public DateOnly? fecha_asignacion { get; set; }
    public decimal valor_compra { get; set; }
    public decimal valor_rescate { get; set; }
    public decimal? vida_util_anios { get; set; }
    public decimal? vida_util_periodos { get; set; }
    public decimal? meses_depreciados { get; set; }
    public bool depreciar { get; set; }
    public DateOnly? fecha_ultima_depreciacion { get; set; }
    public decimal valor_a_depreciar { get; set; }
    public decimal valor_depreciado { get; set; }
    public decimal depreciacion_acumulada { get; set; }
    public decimal depreciacion_mensual { get; set; }
    public decimal depreciacion_diaria { get; set; }
    public decimal valor_libros { get; set; }
    public decimal? valor_libros_alterno { get; set; }
    public string? cuenta_contable { get; set; }
    public string? cuenta_contable_anterior { get; set; }
    public string? cuenta_depreciacion { get; set; }
    public string? cuenta_gasto { get; set; }
    public bool descargado { get; set; }
    public DateOnly? fecha_descargo { get; set; }
    public bool vendido { get; set; }
    public decimal? valor_venta { get; set; }
    public string? observacion { get; set; }

    public virtual ICollection<af_activo_fijo_depreciacion> af_activo_fijo_depreciacions { get; set; } = new List<af_activo_fijo_depreciacion>();
}
