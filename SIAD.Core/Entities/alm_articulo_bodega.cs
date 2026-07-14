using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Ubicación (y a futuro existencia) de un artículo por bodega.
/// Una fila por (artículo, bodega): dónde se guarda y, en Fase 2, cuánto hay.
/// La ubicación física es manual: cinco campos de texto libre (ubicacion1..5).
/// </summary>
public partial class alm_articulo_bodega : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int articulo_id { get; set; }
    public int bodega_id { get; set; }
    public string? ubicacion1 { get; set; }
    public string? ubicacion2 { get; set; }
    public string? ubicacion3 { get; set; }
    public string? ubicacion4 { get; set; }
    public string? ubicacion5 { get; set; }
    public decimal existencia { get; set; }
    public decimal existencia_minima { get; set; }
    public decimal existencia_maxima { get; set; }

    /// <summary>Nivel de existencia al que conviene reponer (disparador de compra). Es configuración: lo teclea el usuario.</summary>
    public decimal punto_reorden { get; set; }

    /// <summary>Stock reservado por requisiciones aprobadas sin despachar. Lo mantiene el servicio (kardex), no se edita a mano.</summary>
    public decimal existencia_comprometida { get; set; }

    /// <summary>Cantidad en camino a esta bodega (orden de compra abierta o traslado). Ingresa a <see cref="existencia"/> al recibir.</summary>
    public decimal existencia_transito { get; set; }

    /// <summary>Costo promedio ponderado del artículo en esta bodega; se recalcula en cada ingreso.</summary>
    public decimal costo_promedio { get; set; }

    /// <summary>Precio unitario de la última compra registrada del artículo en esta bodega (referencia).</summary>
    public decimal ultimo_costo { get; set; }

    public bool principal { get; set; }
    public bool activo { get; set; }
    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual alm_articulo? articulo { get; set; }
    public virtual alm_bodega? bodega { get; set; }
}
