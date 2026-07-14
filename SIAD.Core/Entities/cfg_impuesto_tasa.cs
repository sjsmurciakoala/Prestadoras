using System;

namespace SIAD.Core.Entities;

/// <summary>
/// Tasa de un impuesto CON VIGENCIA. Global (sin <c>company_id</c>), igual que
/// <see cref="cfg_impuesto"/>.
/// <para>
/// POR QUÉ LA VIGENCIA: las tasas cambian por decreto. Cuando eso pasa NO se edita
/// la fila — se cierra su <c>vigencia_hasta</c> y se crea una nueva con el mismo
/// <c>codigo</c>. Si se editara en sitio, reimprimir una factura de 2025 daría el
/// impuesto de hoy: error fiscal grave. La vigencia permite responder "¿qué tasa
/// regía el 3 de marzo?" y reconstruir el pasado con exactitud.
/// </para>
/// <para>
/// Invariantes que la BD impone (ver <c>Database/2026-07-14_cfg_impuestos.sql</c>):
/// <list type="bullet">
///   <item><c>ck_cfg_impuesto_tasa_coherencia</c>: GRAVADO exige porcentaje &gt; 0;
///         EXENTO/EXONERADO exigen porcentaje = 0.</item>
///   <item><c>ck_cfg_impuesto_tasa_vigencia</c>: vigencia_hasta &gt;= vigencia_desde.</item>
///   <item><c>ex_cfg_impuesto_tasa_vigencia</c> (EXCLUDE gist): dos tasas del mismo
///         código NO pueden tener vigencias solapadas (SQLSTATE 23P01).</item>
/// </list>
/// </para>
/// </summary>
public partial class cfg_impuesto_tasa
{
    public int id { get; set; }

    public int impuesto_id { get; set; }

    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    /// <summary>GRAVADO | EXENTO | EXONERADO. Ver <c>SIAD.Core.Constants.TipoImpuestoTasa</c>.</summary>
    public string tipo { get; set; } = null!;

    /// <summary>Porcentaje aplicable. &gt; 0 en GRAVADO, = 0 en EXENTO/EXONERADO.</summary>
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

    public virtual cfg_impuesto? impuesto { get; set; }
}
