using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Relación proveedor ↔ artículo ("UPC"): qué proveedores suministran un artículo,
/// con el código/UPC del proveedor para el artículo, su costo y cuál es el principal.
/// Una fila por (artículo, proveedor). No se elimina: se DESHABILITA (activo=false)
/// para conservar el histórico. Multiempresa vía ICompanyScopedEntity.
/// El proveedor se referencia por código (prv_proveedores es keyless/multiempresa);
/// su existencia se valida en el servicio, no por FK.
/// </summary>
public partial class alm_articulo_proveedor : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int articulo_id { get; set; }
    public string cod_proveedor { get; set; } = null!;
    public string? codigo_upc { get; set; }
    public decimal costo { get; set; }
    public bool principal { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_articulo? articulo { get; set; }
}
