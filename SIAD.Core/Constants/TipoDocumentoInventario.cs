namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>alm_kardex.documento_tipo</c>: qué clase de documento
/// originó el asiento del kardex. Espejo del CHECK <c>ck_alm_kardex_documento_tipo</c>
/// en la base de datos — si se agrega un valor aquí, hay que ampliar el CHECK.
/// </summary>
public static class TipoDocumentoInventario
{
    public const string Compra = "COMPRA";

    /// <summary>
    /// Solicitud de materiales. <b>Declarado y sin productor a propósito:</b> ningún camino de
    /// código escribe asientos con este tipo. La requisición es el pedido; lo que mueve el
    /// inventario es su entrega, que se asienta como <see cref="Descargo"/>. Verificado en el
    /// histórico: requisición, descargo y kardex son el mismo hecho (42.653 pares comunes), y
    /// postear ambos duplicaría la salida.
    /// </summary>
    public const string Requisicion = "REQUISICION";

    /// <summary>Entrega de materiales de bodega: es la SALIDA real que sí se asienta.</summary>
    public const string Descargo = "DESCARGO";
    public const string Traslado = "TRASLADO";
    public const string Ajuste = "AJUSTE";
    public const string CargaInicial = "CARGA_INICIAL";

    /// <summary>
    /// Contra-asiento que anula uno previo. El kardex es inmutable: nada se borra ni se
    /// corrige con UPDATE, se revierte. Agregado al CHECK por
    /// <c>2026-07-30_alm_carga_inicial.sql</c>.
    /// </summary>
    public const string Reversa = "REVERSA";

    /// <summary>Todos los tipos válidos, en el mismo orden que el CHECK de la BD.</summary>
    public static readonly string[] Todos =
    [
        Compra,
        Requisicion,
        Descargo,
        Traslado,
        Ajuste,
        CargaInicial,
        Reversa
    ];

    public static bool EsValido(string? tipo) =>
        !string.IsNullOrWhiteSpace(tipo) && Array.IndexOf(Todos, tipo) >= 0;
}
