namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>alm_compra_hdr.moneda</c>. Espejo del CHECK
/// <c>ck_alm_compra_hdr_moneda</c> en la base de datos — si se agrega un valor aquí, hay
/// que ampliar el CHECK.
/// <para>
/// La 1ª entrega de la recepción de compras opera sólo en Lempiras; el dólar queda
/// declarado para no volver a alterar la tabla cuando se resuelva la multimoneda (D-8).
/// </para>
/// </summary>
public static class MonedaCompra
{
    public const string Lempira = "HNL";
    public const string Dolar = "USD";

    public static readonly string[] Todas = [Lempira, Dolar];

    public static bool EsValida(string? moneda) =>
        !string.IsNullOrWhiteSpace(moneda) && Array.IndexOf(Todas, moneda) >= 0;
}
