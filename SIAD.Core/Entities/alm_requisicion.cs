using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Requisición interna de artículos de almacén (línea por renglón, con datos
/// de cabecera repetidos). Migrado de MySQL bdsimafi.requisiciones.
/// </summary>
public partial class alm_requisicion : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public decimal numero { get; set; }
    public string? codigo_articulo { get; set; }
    public string? descripcion { get; set; }
    public string? aplicacion { get; set; }
    public decimal cantidad { get; set; }
    public decimal precio_unitario { get; set; }
    public decimal valor { get; set; }
    public bool impuesto_aplica { get; set; }
    public decimal impuesto { get; set; }
    public bool descuento_aplica { get; set; }
    public decimal valor_descuento { get; set; }
    public decimal total { get; set; }
    public short tipo_requisicion { get; set; }
    public string? oficina { get; set; }
    public string? departamento { get; set; }
    public string? solicitante { get; set; }
    public string? cargo_solicitante { get; set; }
    public string? diametro { get; set; }
    public string? cuenta_contable { get; set; }
    public string? cuenta_contable_anterior { get; set; }
    public string? cuenta_por_pagar { get; set; }
    public DateOnly? fecha_requisicion { get; set; }
    public DateOnly? fecha_presupuesto { get; set; }
    public DateOnly? fecha_aprobacion { get; set; }
    public DateOnly? fecha_rechazo { get; set; }
    public DateOnly? fecha_entrega { get; set; }
    public bool aprobado { get; set; }
    public bool rechazado { get; set; }
    public bool descargado { get; set; }
    public string? estatus { get; set; }
    public string? observacion { get; set; }
}
