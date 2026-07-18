namespace SIAD.Core.Utilities;

public static class NumerosALetras
{
    private static readonly string[] Unidades =
    [
        "", "UN", "DOS", "TRES", "CUATRO", "CINCO", "SEIS", "SIETE", "OCHO", "NUEVE",
        "DIEZ", "ONCE", "DOCE", "TRECE", "CATORCE", "QUINCE", "DIECISÉIS",
        "DIECISIETE", "DIECIOCHO", "DIECINUEVE"
    ];

    private static readonly string[] Decenas =
    [
        "", "DIEZ", "VEINTE", "TREINTA", "CUARENTA", "CINCUENTA",
        "SESENTA", "SETENTA", "OCHENTA", "NOVENTA"
    ];

    private static readonly string[] Centenas =
    [
        "", "CIEN", "DOSCIENTOS", "TRESCIENTOS", "CUATROCIENTOS", "QUINIENTOS",
        "SEISCIENTOS", "SETECIENTOS", "OCHOCIENTOS", "NOVECIENTOS"
    ];

    private static readonly string[] Veintiuno =
    [
        "", "VEINTIÚN", "VEINTIDÓS", "VEINTITRÉS", "VEINTICUATRO", "VEINTICINCO",
        "VEINTISÉIS", "VEINTISIETE", "VEINTIOCHO", "VEINTINUEVE"
    ];

    public static string Convertir(decimal numero)
    {
        var entera = (long)Math.Floor(numero);
        var cents = (int)Math.Round((numero - Math.Floor(numero)) * 100);
        var letras = EnterALetras(entera);
        return $"{letras} CON {cents:D2}/100";
    }

    private static string EnterALetras(long n)
    {
        if (n == 0) return "CERO";
        if (n < 0) return "MENOS " + EnterALetras(-n);

        var resultado = string.Empty;

        if (n >= 1_000_000)
        {
            var millones = n / 1_000_000;
            resultado += millones == 1
                ? "UN MILLÓN "
                : EnterALetras(millones) + " MILLONES ";
            n %= 1_000_000;
        }

        if (n >= 1_000)
        {
            var miles = n / 1_000;
            resultado += miles == 1
                ? "MIL "
                : EnterALetras(miles) + " MIL ";
            n %= 1_000;
        }

        if (n >= 100)
        {
            var c = (int)(n / 100);
            // 100 exacto es "CIEN"; 101-199 se dice "CIENTO ..." (no "CIEN ...").
            resultado += n == 100
                ? "CIEN "
                : (c == 1 ? "CIENTO " : Centenas[c] + " ");
            n %= 100;
        }

        if (n >= 20)
        {
            var d = (int)(n / 10);
            var u = (int)(n % 10);
            if (d == 2 && u > 0)
                resultado += Veintiuno[u] + " ";
            else
                resultado += Decenas[d] + (u > 0 ? " Y " + Unidades[u] : "") + " ";
        }
        else if (n > 0)
        {
            resultado += Unidades[(int)n] + " ";
        }

        return resultado.Trim();
    }
}
