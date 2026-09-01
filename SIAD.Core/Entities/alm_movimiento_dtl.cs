using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Renglón del movimiento de almacén. <b>Es la unidad de posteo</b>, no la cabecera:
/// cada uno deriva su propio <see cref="uuid"/> v5 determinista, así que un reintento no
/// duplica un renglón ya insertado mientras otro falló. Mismo patrón que
/// <c>alm_compra</c> y <c>alm_descargo</c>.
/// </summary>
public partial class alm_movimiento_dtl : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int movimiento_hdr_id { get; set; }
    public int articulo_id { get; set; }

    /// <summary>Snapshot del código al capturar: el maestro puede cambiar después.</summary>
    public string? codigo_articulo { get; set; }

    public decimal cantidad { get; set; }

    /// <summary>
    /// Costo tecleado por el usuario. <b>Sólo aplica en clase ENTRADA</b>; en SALIDA el motor
    /// valoriza al costo promedio vigente del par y este campo queda en 0.
    /// <para>
    /// Es la traducción de <c>PIDE_COSTO</c> de <c>INV_TRANSACC_AXL</c>, que en el legacy vale
    /// 0 para toda salida.
    /// </para>
    /// </summary>
    public decimal costo_unitario { get; set; }

    /// <summary>
    /// Costo con el que el motor efectivamente valorizó el asiento, copiado tras postear.
    /// Evita un JOIN contra <c>alm_kardex</c> en cada lectura del documento.
    /// </summary>
    public decimal? costo_real { get; set; }

    public decimal total { get; set; }

    /// <summary>
    /// Traslado: suma de lo ya recibido en destino de este renglón (crece con cada recepción
    /// parcial). En tránsito del renglón = <see cref="cantidad"/> − <see cref="cantidad_recibida"/>.
    /// 0 en entradas/salidas normales. Un CHECK garantiza 0 ≤ recibida ≤ cantidad.
    /// </summary>
    public decimal cantidad_recibida { get; set; }

    /// <summary>Asiento generado. En un traslado, es el asiento de SALIDA de origen (el envío).
    /// NULL mientras no esté posteado (lo impone un CHECK).</summary>
    public int? kardex_id { get; set; }

    /// <summary>Idempotencia del renglón: uuid v5 derivado de (documento, línea).</summary>
    public Guid uuid { get; set; }

    public bool posteado { get; set; }

    public virtual alm_movimiento_hdr cabecera { get; set; } = null!;
}
