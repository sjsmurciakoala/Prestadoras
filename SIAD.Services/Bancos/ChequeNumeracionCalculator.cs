namespace SIAD.Services.Bancos;

/// <summary>Resultado del calculo de numeracion de un cheque.</summary>
public readonly record struct ChequeNumeracionResult(decimal NumeroAsignado, decimal SiguienteProximo, bool Agotado);

/// <summary>
/// Logica pura (sin BD) de la numeracion de cheques por cuenta.
/// proximo_cheque llega como NUMERIC(28,4) migrado de SIMAFI (puede traer
/// decimales): se trunca a entero. proximo <= 0 se normaliza a 1.
/// chequeMaximo = 0 significa "sin limite" (no se valida agotamiento).
/// </summary>
public static class ChequeNumeracionCalculator
{
    public static ChequeNumeracionResult Compute(decimal proximoCheque, decimal chequeMaximo)
    {
        var numero = decimal.Truncate(proximoCheque);
        if (numero < 1m)
        {
            numero = 1m;
        }

        var maximo = decimal.Truncate(chequeMaximo);
        var agotado = maximo > 0m && numero > maximo;

        return new ChequeNumeracionResult(numero, numero + 1m, agotado);
    }
}
