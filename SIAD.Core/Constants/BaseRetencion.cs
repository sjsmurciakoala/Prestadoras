namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>cfg_retencion.base_calculo</c>. Espejo del CHECK
/// <c>ck_cfg_retencion_base</c> en la base de datos — si se agrega un valor aquí, hay que ampliar
/// el CHECK.
/// <para>
/// Define sobre qué monto se calcula la retención:
/// <list type="bullet">
///   <item><b>SIN_ISV</b> — sobre el subtotal (sin ISV). Es la base del ISR (Art. 50 / Acuerdo 217-2010).</item>
///   <item><b>TOTAL</b> — sobre el monto con ISV.</item>
/// </list>
/// </para>
/// <para>
/// TODO (base ISV, F2/D2): para la retención de ISV, expresar la tasa como % sobre <c>SIN_ISV</c>
/// solo equivale a "% del ISV" cuando el ISV es 15% (11.25% sobre subtotal == 75% del ISV al 15%).
/// Si se retiene sobre ventas al 18% o cambia la tasa de ISV, revisar si hace falta un tercer valor
/// (p. ej. <c>ISV</c> = base es el monto del impuesto). A confirmar con el contador.
/// </para>
/// </summary>
public static class BaseRetencion
{
    public const string Total = "TOTAL";
    public const string SinIsv = "SIN_ISV";

    /// <summary>Todos los valores válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Total,
        SinIsv
    ];

    public static bool EsValido(string? baseCalculo) =>
        !string.IsNullOrWhiteSpace(baseCalculo) && Array.IndexOf(Todos, baseCalculo) >= 0;
}
