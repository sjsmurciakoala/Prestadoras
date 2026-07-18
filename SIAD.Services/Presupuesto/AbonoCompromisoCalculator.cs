namespace SIAD.Services.Presupuesto;

/// <summary>Estado minimo de una fila de abono para el calculo de saldo.</summary>
public readonly record struct AbonoLineaState(int NumeroAbono, decimal Monto, string? Estado);

/// <summary>Resultado del calculo de saldo y numeracion de abonos de un compromiso.</summary>
public readonly record struct AbonoComputeResult(decimal SaldoActual, int SiguienteNumeroAbono);

/// <summary>
/// Logica pura (sin base de datos) del saldo pendiente y el siguiente numero_abono.
/// El saldo NO se persiste: se deriva de (monto - SUM(abonos vigentes)).
/// Convencion de estado: 'V' vigente, 'A' anulado.
/// </summary>
public static class AbonoCompromisoCalculator
{
    public const string EstadoVigente = "V";
    public const string EstadoAnulado = "A";

    public static AbonoComputeResult Compute(
        decimal montoCompromiso,
        bool procesadoLegacy,
        IReadOnlyCollection<AbonoLineaState> abonos)
    {
        ArgumentNullException.ThrowIfNull(abonos);

        var totalVigente = abonos
            .Where(a => string.Equals(a.Estado, EstadoVigente, StringComparison.OrdinalIgnoreCase))
            .Sum(a => a.Monto);

        // Compat: compromiso legacy procesado y SIN filas de abono => saldo 0.
        var saldo = (abonos.Count == 0 && procesadoLegacy)
            ? 0m
            : montoCompromiso - totalVigente;

        if (saldo < 0m)
        {
            saldo = 0m;
        }

        // numero_abono corre sobre TODAS las filas (incluidas anuladas) para no reusar numeros.
        var siguiente = (abonos.Count == 0 ? 0 : abonos.Max(a => a.NumeroAbono)) + 1;

        return new AbonoComputeResult(saldo, siguiente);
    }
}
