using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IKardexService
{
    /// <summary>
    /// Devuelve el kardex de un artículo con saldo corrido. Null si el código
    /// no corresponde a ningún artículo del catálogo.
    /// </summary>
    Task<KardexArticuloDto?> GetByArticuloAsync(KardexFilterDto filtro, CancellationToken ct = default);

    /// <summary>Tipos de transacción presentes en el kardex, con etiqueta legible.</summary>
    Task<IReadOnlyList<TipoMovimientoDto>> GetTiposMovimientoAsync(CancellationToken ct = default);
}
