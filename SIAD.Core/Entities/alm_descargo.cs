using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Descargo (salida/consumo) de artículo de almacén hacia un departamento.
/// Migrado de MySQL bdsimafi.descargos.
/// </summary>
public partial class alm_descargo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public DateOnly? fecha { get; set; }
    public string? codigo_articulo { get; set; }
    public decimal cantidad { get; set; }
    public decimal precio_unitario { get; set; }
    public decimal total { get; set; }
    public string? oficina { get; set; }
    public string? departamento { get; set; }
    public decimal? numero_requisicion { get; set; }
    public decimal? numero_documento { get; set; }
    public short? tipo_requisicion { get; set; }
    public string? traslado { get; set; }
    public string? cuenta_contable_1 { get; set; }
    public string? cuenta_contable_1_detalle { get; set; }
    public string? cuenta_contable_2 { get; set; }
    public string? cuenta_contable_2_detalle { get; set; }
    public string? comentario { get; set; }
}
