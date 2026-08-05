using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Documento de ajuste de inventario: la vía legítima de mover stock una vez que se cierre
/// la captura manual de existencia. Sin él, el almacén quedaría en solo lectura hasta que
/// exista el módulo de compras.
/// <para>
/// Cabecera plana (una línea por documento). La numeración formal y el catálogo de motivos
/// son trabajo posterior; ampliarlo después no rompe nada.
/// </para>
/// </summary>
public partial class alm_ajuste_inventario : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    public int articulo_id { get; set; }
    public int bodega_id { get; set; }

    /// <summary>
    /// <c>ENTRADA</c> suma existencia (sobrante de conteo), <c>SALIDA</c> resta
    /// (faltante/merma), <c>VALOR</c> solo corrige el costo sin mover existencia.
    /// Ver <see cref="SIAD.Core.Constants.ClaseAjusteInventario"/>.
    /// </summary>
    public string clase { get; set; } = null!;

    /// <summary>Positiva en ENTRADA/SALIDA; 0 en VALOR (lo impone un CHECK en la BD).</summary>
    public decimal cantidad { get; set; }

    /// <summary>
    /// Obligatorio (&gt; 0) en ENTRADA y VALOR. En SALIDA se ignora: el motor usa el costo
    /// promedio vigente, porque una salida no cambia el promedio.
    /// </summary>
    public decimal costo_unitario { get; set; }

    /// <summary>Texto libre, obligatorio y no vacío.</summary>
    public string motivo { get; set; } = null!;

    public string? observacion { get; set; }

    /// <summary>true = ya tiene su asiento en el kardex. Se marca en la MISMA transacción del posteo.</summary>
    public bool posteado { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }

    public virtual alm_articulo? articulo { get; set; }
    public virtual alm_bodega? bodega { get; set; }
}
