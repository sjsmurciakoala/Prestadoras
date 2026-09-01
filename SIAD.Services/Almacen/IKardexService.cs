using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Common;

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

    /// <summary>
    /// Libro de movimientos de una bodega: una página de asientos de kardex de la bodega
    /// (todos los artículos) para el filtro/período, con paginación y orden en el servidor.
    /// </summary>
    Task<PagedResult<MovimientoBodegaItemDto>> GetMovimientosBodegaPagedAsync(
        MovimientosBodegaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default);

    /// <summary>Totales (movimientos, ingresos, salidas, valor) del libro de bodega para el filtro.</summary>
    Task<MovimientosBodegaResumenDto> GetResumenBodegaAsync(MovimientosBodegaFilterDto filtro, CancellationToken ct = default);

    /// <summary>Libro de movimientos de una bodega COMPLETO (sin paginar, orden cronológico) para el PDF.</summary>
    Task<IReadOnlyList<MovimientoBodegaItemDto>> GetMovimientosBodegaAsync(MovimientosBodegaFilterDto filtro, CancellationToken ct = default);

    /// <summary>Datos para imprimir el kardex de un artículo en PDF (empresa + movimientos con saldo corrido).</summary>
    Task<MovimientosKardexImpresionDto> GetDatosImpresionArticuloAsync(KardexFilterDto filtro, string impresoPor, CancellationToken ct = default);

    /// <summary>Datos para imprimir el libro de movimientos de una bodega en PDF (empresa + movimientos).</summary>
    Task<MovimientosKardexImpresionDto> GetDatosImpresionBodegaAsync(MovimientosBodegaFilterDto filtro, string impresoPor, CancellationToken ct = default);
}
