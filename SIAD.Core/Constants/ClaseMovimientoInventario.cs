namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>alm_tipo_movimiento.clase</c>. Espejo del CHECK
/// <c>ck_alm_tipo_movimiento_clase</c>: si se agrega un valor aquí, hay que ampliar el CHECK.
/// <para>
/// <b>Es distinto de <see cref="ClaseAjusteInventario"/> a propósito.</b> Aquel es el espejo del
/// CHECK de <c>alm_ajuste_inventario</c> (ajuste de una sola línea) y se queda en
/// ENTRADA/SALIDA/VALOR. Este catálogo de movimientos multi-renglón añade <see cref="Traslado"/>,
/// que un ajuste de una línea no admite: mezclarlos haría que <c>AjusteInventarioService</c>
/// aceptara una clase que su propia tabla rechaza (violación del CHECK en tiempo de INSERT).
/// </para>
/// </summary>
public static class ClaseMovimientoInventario
{
    /// <summary>Suma existencia y recalcula el costo promedio.</summary>
    public const string Entrada = "ENTRADA";

    /// <summary>Resta existencia al costo promedio vigente, sin moverlo.</summary>
    public const string Salida = "SALIDA";

    /// <summary>Corrige solo el costo; no mueve existencia (cantidad = 0).</summary>
    public const string Valor = "VALOR";

    /// <summary>Movimiento entre dos bodegas: sale de origen y entra a destino (Fase 5).</summary>
    public const string Traslado = "TRASLADO";

    public static readonly string[] Todas = [Entrada, Salida, Valor, Traslado];

    public static bool EsValida(string? clase) =>
        !string.IsNullOrWhiteSpace(clase) && Array.IndexOf(Todas, clase) >= 0;
}
