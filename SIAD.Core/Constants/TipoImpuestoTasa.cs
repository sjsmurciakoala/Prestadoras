namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>cfg_impuesto_tasa.tipo</c>. Espejo del CHECK
/// <c>ck_cfg_impuesto_tasa_tipo</c> en la base de datos — si se agrega un valor
/// aquí, hay que ampliar el CHECK.
/// <para>
/// Los tres NO son lo mismo y el SAR los declara por separado (formulario SAR 222
/// tiene renglones distintos):
/// <list type="bullet">
///   <item><b>GRAVADO</b> — paga ISV (15% general, 18% selectivo).</item>
///   <item><b>EXENTO</b> — no paga por LEY (Art. 15: agua potable y alcantarillado,
///         energía, medicinas, canasta básica, educación, salud, libros).</item>
///   <item><b>EXONERADO</b> — pagaría, pero el SAR otorga exoneración por resolución.</item>
/// </list>
/// Por eso el tipo es una columna y no se deduce de "porcentaje = 0".
/// </para>
/// </summary>
public static class TipoImpuestoTasa
{
    public const string Gravado = "GRAVADO";
    public const string Exento = "EXENTO";
    public const string Exonerado = "EXONERADO";

    /// <summary>Todos los tipos válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Gravado,
        Exento,
        Exonerado
    ];

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Array.IndexOf(Todos, tipo) >= 0;

    /// <summary>
    /// true si el tipo exige porcentaje &gt; 0 (GRAVADO). EXENTO y EXONERADO exigen
    /// porcentaje = 0. Espejo de <c>ck_cfg_impuesto_tasa_coherencia</c>.
    /// </summary>
    public static bool ExigePorcentaje(string? tipo) =>
        string.Equals(tipo, Gravado, StringComparison.OrdinalIgnoreCase);
}
