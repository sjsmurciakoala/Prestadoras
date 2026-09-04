using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Mantenimiento del catálogo de tipos de movimiento de almacén (<c>alm_tipo_movimiento</c>).
/// Es el equivalente configurable de <c>INV_TIPOSTRANSACC</c> de Centura: el usuario da de alta
/// tipos de negocio sin recompilar. La <c>clase</c> (ENTRADA/SALIDA/VALOR) es lo único que el
/// motor de posteo interpreta.
/// </summary>
public interface ITipoMovimientoService
{
    /// <summary>Catálogo de la empresa actual, ordenado por <c>orden</c> y código.</summary>
    /// <param name="soloActivos">true = sólo los tipos usables en captura.</param>
    Task<IReadOnlyList<TipoMovimientoAlmacenListItemDto>> GetAsync(bool soloActivos, CancellationToken ct = default);

    Task<TipoMovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<TipoMovimientoAlmacenDto> CrearAsync(TipoMovimientoAlmacenDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Actualiza el tipo. La <c>clase</c> NO se puede cambiar si el tipo ya tiene movimientos
    /// posteados (reinterpretaría retroactivamente los asientos pasados).
    /// </summary>
    Task<TipoMovimientoAlmacenDto> ActualizarAsync(int id, TipoMovimientoAlmacenDto dto, string user, CancellationToken ct = default);

    /// <summary>Desactiva; NO borra. El histórico sigue resolviendo un tipo desactivado.</summary>
    Task<bool> DesactivarAsync(int id, string user, CancellationToken ct = default);
}
