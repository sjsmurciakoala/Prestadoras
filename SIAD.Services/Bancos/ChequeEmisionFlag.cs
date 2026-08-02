namespace SIAD.Services.Bancos;

/// <summary>
/// Interpretacion del flag <c>ban_tipos_transacciones.emite_cheque</c>, que en la BD
/// migrada de SIMAFI viene como texto de 1 caracter con varias convenciones.
/// </summary>
public static class ChequeEmisionFlag
{
    public static bool EsAfirmativo(string? valor)
    {
        var flag = valor?.Trim().ToUpperInvariant();
        return flag is "S" or "Y" or "1" or "T" or "TRUE";
    }
}
