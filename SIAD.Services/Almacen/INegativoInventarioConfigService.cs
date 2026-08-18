using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Interruptor por empresa para permitir existencia NEGATIVA en salidas (cfg_inventario_negativo).
/// Sirve a la pantalla de configuración; la RESOLUCIÓN efectiva (empresa + override por bodega) la
/// hace el motor de posteo por su cuenta (<c>InventarioPostingService.PermiteNegativoAsync</c>).
/// </summary>
public interface INegativoInventarioConfigService
{
    /// <summary>Devuelve el interruptor de la empresa actual. Sin fila → <c>false</c> (no la crea).</summary>
    Task<NegativoInventarioConfigDto> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Guarda el interruptor de la empresa actual (crea la fila si no existe).</summary>
    Task<NegativoInventarioConfigDto> GuardarAsync(NegativoInventarioConfigDto dto, string user, CancellationToken ct = default);
}
