using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Ubicación (y a futuro existencia) de un artículo por bodega.
/// Una fila por (artículo, bodega): dónde se guarda y, en Fase 2, cuánto hay.
/// </summary>
public partial class alm_articulo_bodega : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int articulo_id { get; set; }
    public int bodega_id { get; set; }
    public int? estante_id { get; set; }
    public decimal existencia { get; set; }
    public decimal existencia_minima { get; set; }
    public bool principal { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_articulo? articulo { get; set; }
    public virtual alm_bodega? bodega { get; set; }
    public virtual alm_estante? estante { get; set; }
}
