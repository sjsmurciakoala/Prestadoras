using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Ajustes de inventario. Es la contraparte imprescindible del cierre de la captura
/// manual de existencia: sin esto, el almacén se quedaría sin NINGUNA forma de registrar
/// una entrada o salida de stock hasta que exista el módulo de compras.
/// </summary>
public interface IAjusteInventarioService
{
    /// <summary>
    /// Crea el documento, postea su asiento en el kardex y lo marca como posteado, todo
    /// en una transacción. Devuelve el DTO con el resultado del posteo.
    /// </summary>
    Task<AjusteInventarioDto> CrearYPostearAsync(AjusteInventarioDto dto, string user, CancellationToken ct = default);

    /// <summary>Ajustes de un par (artículo, bodega), del más reciente al más viejo.</summary>
    Task<IReadOnlyList<AjusteInventarioDto>> GetPorParAsync(int articuloId, int bodegaId, CancellationToken ct = default);
}
