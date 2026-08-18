namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Interruptor MAESTRO de la empresa para permitir existencia NEGATIVA en salidas de inventario
/// (<c>cfg_inventario_negativo</c>). El override por bodega
/// (<c>alm_bodega.permite_existencia_negativa</c>) se edita desde el mantenimiento de bodegas.
/// </summary>
public sealed class NegativoInventarioConfigDto
{
    /// <summary>
    /// <c>true</c> = las salidas de la empresa pueden dejar la existencia en negativo (salvo que
    /// una bodega lo fuerce a <c>false</c>). <c>false</c> (default) = el motor bloquea toda salida
    /// que cruce a negativo.
    /// </summary>
    public bool Permitir { get; set; }
}
