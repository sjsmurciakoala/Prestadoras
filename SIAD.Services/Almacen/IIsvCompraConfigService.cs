using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Configuración del ISV en compras a nivel de empresa (cfg_compra_isv): el tratamiento
/// (al costo / impuesto fiscal) que aplica a todas las compras de la empresa actual.
/// </summary>
public interface IIsvCompraConfigService
{
    /// <summary>
    /// Devuelve el tratamiento configurado para la empresa actual. Si no hay fila, devuelve
    /// el valor por defecto (COSTO) sin crearla.
    /// </summary>
    Task<IsvCompraConfigDto> ObtenerAsync(CancellationToken ct = default);

    /// <summary>Guarda el tratamiento de la empresa actual (crea la fila si no existe).</summary>
    Task<IsvCompraConfigDto> GuardarAsync(IsvCompraConfigDto dto, string user, CancellationToken ct = default);
}
