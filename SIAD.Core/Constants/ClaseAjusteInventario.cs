namespace SIAD.Core.Constants;

/// <summary>
/// Vocabulario cerrado de <c>alm_ajuste_inventario.clase</c>. Espejo del CHECK
/// <c>ck_alm_ajuste_inventario_clase</c>: si se agrega un valor aquí, hay que ampliar
/// el CHECK con un script.
/// </summary>
public static class ClaseAjusteInventario
{
    /// <summary>Suma existencia: sobrante de conteo físico, devolución.</summary>
    public const string Entrada = "ENTRADA";

    /// <summary>Resta existencia: faltante de conteo físico, merma.</summary>
    public const string Salida = "SALIDA";

    /// <summary>Solo corrige el costo; no mueve existencia (cantidad = 0).</summary>
    public const string Valor = "VALOR";

    public static readonly string[] Todas = [Entrada, Salida, Valor];

    public static bool EsValida(string? clase) =>
        !string.IsNullOrWhiteSpace(clase) && Array.IndexOf(Todas, clase) >= 0;
}
