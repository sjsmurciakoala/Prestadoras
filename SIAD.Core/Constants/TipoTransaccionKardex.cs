namespace SIAD.Core.Constants;

/// <summary>
/// Códigos legacy de <c>alm_kardex.tipo_transaccion</c>, heredados de SIMAFI. Son texto
/// libre en la BD (esa columna NO tiene CHECK), así que la regla vive aquí.
/// <para>
/// No confundir con <see cref="TipoDocumentoInventario"/>: ese dice QUÉ DOCUMENTO originó
/// el asiento; este es la clasificación numérica antigua que sigue alimentando el combo
/// de la pantalla de kardex (que se arma con un DISTINCT sobre los valores existentes).
/// </para>
/// </summary>
public static class TipoTransaccionKardex
{
    /// <summary>Entrada. En el legacy también significa "inventario inicial", por eso la apertura lo usa.</summary>
    public const string EntradaInventarioInicial = "102";

    /// <summary>Ajuste de inventario.</summary>
    public const string Ajuste = "103";

    /// <summary>Salida. La reversa de una entrada se postea con este código.</summary>
    public const string Salida = "202";
}
