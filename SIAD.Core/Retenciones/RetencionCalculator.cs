using SIAD.Core.Constants;

namespace SIAD.Core.Retenciones;

/// <summary>
/// Resultado del autocálculo de una retención (puro, sin BD).
/// </summary>
/// <param name="PuedeCalcularMonto">
/// <c>false</c> cuando la retención NO tiene una tasa vigente (p. ej. ISV-RET, pendiente de confirmar
/// con la SAR): en ese caso <see cref="Monto"/> es 0 y el usuario debe capturarlo a mano. NUNCA se
/// inventa un porcentaje.
/// </param>
/// <param name="Base">Base imponible propuesta (redondeada a 2 decimales). Editable en la UI.</param>
/// <param name="Monto">Monto a retener = Base × % / 100 (redondeado a 2). 0 si no hay tasa vigente.</param>
public readonly record struct RetencionCalculoResult(bool PuedeCalcularMonto, decimal Base, decimal Monto);

/// <summary>
/// Aritmética PURA del autocálculo de retenciones a proveedores (F2). Sin base de datos: la UI le pasa
/// el bruto, la base de cálculo, el % vigente (o null) y la tasa ISV general vigente, y recibe la base y
/// el monto. Vive en <c>SIAD.Core</c> para poder reusarse desde el cliente Blazor y desde los tests.
/// <para>
/// Reglas (régimen SAR / doc de retenciones §D3):
/// <list type="bullet">
///   <item>Base <b>SIN_ISV</b>: <c>bruto / (1 + tasaISV/100)</c> — le quita el ISV general al bruto. Si
///   NO hay tasa ISV vigente, el divisor es 1 (base = bruto) y quien llama debe AVISARLO (ver
///   <see cref="RequiereTasaIsvYNoHay"/>): la base no se redujo y podría estar inflada.</item>
///   <item>Base <b>TOTAL</b> (o desconocida): base = bruto.</item>
///   <item>Monto = <c>base × % / 100</c>, redondeo a 2 decimales <b>AwayFromZero</b> (convención monetaria
///   del proyecto, igual que <c>CobroService</c>).</item>
///   <item>Porcentaje <c>null</c> o ≤ 0 ⇒ no se calcula monto (no se inventa %).</item>
/// </list>
/// </para>
/// </summary>
public static class RetencionCalculator
{
    /// <summary>
    /// Calcula la base y el monto de una retención. <paramref name="porcentaje"/> null/≤0 ⇒
    /// <see cref="RetencionCalculoResult.PuedeCalcularMonto"/> = false y monto 0.
    /// </summary>
    public static RetencionCalculoResult Calcular(
        decimal bruto,
        string? baseCalculo,
        decimal? porcentaje,
        decimal? tasaIsvPorcentaje)
    {
        var baseImponible = CalcularBase(bruto, baseCalculo, tasaIsvPorcentaje);
        var puede = porcentaje is > 0m;
        var monto = MontoDesdeBase(baseImponible, porcentaje);
        return new RetencionCalculoResult(puede, baseImponible, monto);
    }

    /// <summary>
    /// Base imponible según <paramref name="baseCalculo"/>. SIN_ISV le quita el ISV general al bruto;
    /// TOTAL (o cualquier otro valor) devuelve el bruto. Redondea a 2 decimales.
    /// </summary>
    public static decimal CalcularBase(decimal bruto, string? baseCalculo, decimal? tasaIsvPorcentaje)
    {
        if (bruto <= 0m) return 0m;

        if (EsSinIsv(baseCalculo))
        {
            var isv = tasaIsvPorcentaje.GetValueOrDefault();
            var divisor = isv > 0m ? 1m + isv / 100m : 1m; // sin ISV vigente ⇒ divisor 1 (base = bruto)
            return Redondear2(bruto / divisor);
        }

        return Redondear2(bruto);
    }

    /// <summary>
    /// Monto = base × % / 100, redondeado a 2 (AwayFromZero). <paramref name="porcentaje"/> null/≤0 ⇒ 0.
    /// Se expone aparte para recalcular el monto cuando el usuario EDITA la base a mano.
    /// </summary>
    public static decimal MontoDesdeBase(decimal baseImponible, decimal? porcentaje)
    {
        if (porcentaje is not { } pct || pct <= 0m) return 0m;
        return Redondear2(baseImponible * pct / 100m);
    }

    /// <summary>
    /// true cuando la base es SIN_ISV pero NO hay tasa ISV vigente: la base quedó igual al bruto (sin
    /// reducir). La UI debe avisarlo explícitamente para que el usuario no aplique una base inflada.
    /// </summary>
    public static bool RequiereTasaIsvYNoHay(string? baseCalculo, decimal? tasaIsvPorcentaje)
        => EsSinIsv(baseCalculo) && !(tasaIsvPorcentaje is > 0m);

    private static bool EsSinIsv(string? baseCalculo)
        => string.Equals(baseCalculo?.Trim(), BaseRetencion.SinIsv, StringComparison.OrdinalIgnoreCase);

    private static decimal Redondear2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
}
