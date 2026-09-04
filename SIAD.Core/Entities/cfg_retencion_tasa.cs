using System;

namespace SIAD.Core.Entities;

/// <summary>
/// Tasa de una retención CON VIGENCIA. Global (sin <c>company_id</c>), igual que
/// <see cref="cfg_retencion"/>.
/// <para>
/// A diferencia de <see cref="cfg_impuesto_tasa"/>, NO lleva <c>codigo</c>/<c>nombre</c>/<c>tipo</c>:
/// la retención ES el concepto (identificado por <see cref="cfg_retencion.codigo"/>); esta tabla
/// es solo su porcentaje a lo largo del tiempo. Por eso el EXCLUDE de no-solape es por
/// <c>retencion_id</c> (una sola tasa vigente por retención en cada fecha).
/// </para>
/// <para>
/// POR QUÉ LA VIGENCIA: los porcentajes cambian por decreto. Cuando eso pasa NO se edita la
/// fila — se cierra su <c>vigencia_hasta</c> y se crea una nueva. Así se puede responder "¿qué %
/// regía el 3 de marzo?" y reconstruir el pasado con exactitud.
/// </para>
/// <para>
/// Invariantes que la BD impone (ver <c>Database/2026-08-06_cfg_retenciones.sql</c>):
/// <list type="bullet">
///   <item><c>ck_cfg_retencion_tasa_rango</c>: porcentaje &gt; 0 y &lt;= 100 (una retención
///         siempre retiene algo).</item>
///   <item><c>ck_cfg_retencion_tasa_vigencia</c>: vigencia_hasta &gt;= vigencia_desde.</item>
///   <item><c>ex_cfg_retencion_tasa_vigencia</c> (EXCLUDE gist): dos tasas de la misma retención
///         NO pueden tener vigencias solapadas (SQLSTATE 23P01).</item>
/// </list>
/// </para>
/// </summary>
public partial class cfg_retencion_tasa
{
    public int id { get; set; }

    public int retencion_id { get; set; }

    /// <summary>Porcentaje a retener. Estrictamente &gt; 0.</summary>
    public decimal porcentaje { get; set; }

    public DateOnly vigencia_desde { get; set; }

    /// <summary>NULL = vigente indefinidamente.</summary>
    public DateOnly? vigencia_hasta { get; set; }

    public string? descripcion { get; set; }

    public bool activo { get; set; } = true;

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual cfg_retencion? retencion { get; set; }
}
