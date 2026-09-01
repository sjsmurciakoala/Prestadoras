using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IValuacionInventarioService
{
    /// <summary>
    /// Valuación del inventario a una fecha de corte: existencia y costo por (artículo, bodega)
    /// según el último asiento del kardex con snapshot hasta esa fecha. Reutiliza
    /// <see cref="ExistenciaBodegaItemDto"/> (mismas columnas que el reporte de existencias).
    /// </summary>
    Task<IReadOnlyList<ExistenciaBodegaItemDto>> GetAsync(ValuacionInventarioFilterDto filtro, CancellationToken ct = default);

    /// <summary>Datos para imprimir el reporte en PDF (encabezado de empresa + líneas + título con la fecha de corte).</summary>
    Task<ExistenciasBodegaImpresionDto> GetDatosImpresionAsync(ValuacionInventarioFilterDto filtro, string impresoPor, CancellationToken ct = default);
}
