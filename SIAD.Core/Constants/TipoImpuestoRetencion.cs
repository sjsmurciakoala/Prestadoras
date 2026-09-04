namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>cfg_retencion.tipo_impuesto</c>. Espejo del CHECK
/// <c>ck_cfg_retencion_tipo_impuesto</c> en la base de datos — si se agrega un valor aquí, hay que
/// ampliar el CHECK.
/// <para>
/// Qué impuesto se retiene. El SAR los entera y declara por separado:
/// <list type="bullet">
///   <item><b>ISR</b> — retención de Impuesto Sobre la Renta (sobre la renta del proveedor).</item>
///   <item><b>ISV</b> — retención de Impuesto Sobre Ventas (sobre el ISV de ciertos servicios).</item>
/// </list>
/// </para>
/// </summary>
public static class TipoImpuestoRetencion
{
    public const string Isr = "ISR";
    public const string Isv = "ISV";

    /// <summary>Todos los valores válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Isr,
        Isv
    ];

    public static bool EsValido(string? tipoImpuesto) =>
        !string.IsNullOrWhiteSpace(tipoImpuesto) && Array.IndexOf(Todos, tipoImpuesto) >= 0;
}
