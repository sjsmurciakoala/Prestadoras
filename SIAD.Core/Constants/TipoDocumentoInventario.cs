namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>alm_kardex.documento_tipo</c>: qué clase de documento
/// originó el asiento del kardex. Espejo del CHECK <c>ck_alm_kardex_documento_tipo</c>
/// en la base de datos — si se agrega un valor aquí, hay que ampliar el CHECK.
/// </summary>
public static class TipoDocumentoInventario
{
    public const string Compra = "COMPRA";
    public const string Requisicion = "REQUISICION";
    public const string Descargo = "DESCARGO";
    public const string Traslado = "TRASLADO";
    public const string Ajuste = "AJUSTE";
    public const string CargaInicial = "CARGA_INICIAL";

    /// <summary>Todos los tipos válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Compra,
        Requisicion,
        Descargo,
        Traslado,
        Ajuste,
        CargaInicial
    ];

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Array.IndexOf(Todos, tipo) >= 0;
}
