using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Compra de artículo de almacén (línea por artículo, con datos de
/// proveedor/factura repetidos por fila). Migrado de MySQL bdsimafi.compras.
/// </summary>
public partial class alm_compra : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public DateOnly? fecha { get; set; }
    public DateOnly? fecha_factura { get; set; }
    public string? codigo_articulo { get; set; }
    public decimal cantidad { get; set; }
    public decimal precio_unitario { get; set; }
    public decimal precio_unitario_anterior { get; set; }
    public decimal total { get; set; }
    public decimal impuesto { get; set; }
    public decimal descuento { get; set; }
    public string? oficina { get; set; }
    public string? proveedor { get; set; }
    public decimal? numero_factura { get; set; }
    public decimal? numero { get; set; }
    public string? orden_compra { get; set; }
    public decimal? plazo_dias { get; set; }
    public short tipo_compra { get; set; }
    public string? traslado { get; set; }
    public string? cuenta_contable { get; set; }
    public string? cuenta_contable_anterior { get; set; }
    public string? cuenta_por_pagar { get; set; }
    public string? cuenta_por_pagar_anterior { get; set; }
    public string? codigo_compra { get; set; }
    public string? concepto { get; set; }
}
