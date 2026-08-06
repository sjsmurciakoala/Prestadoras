using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

public interface IExistenciasBodegaService
{
    /// <summary>
    /// Existencias por (artículo, bodega) valorizadas al costo promedio de cada bodega, para
    /// el reporte agrupado por bodega. Orden: bodega, luego artículo. Alimenta la pantalla y el PDF.
    /// </summary>
    Task<IReadOnlyList<ExistenciaBodegaItemDto>> GetAsync(ExistenciasBodegaFilterDto filtro, CancellationToken ct = default);

    /// <summary>Datos para imprimir el reporte en PDF: encabezado de empresa + líneas + texto de filtros.</summary>
    Task<ExistenciasBodegaImpresionDto> GetDatosImpresionAsync(ExistenciasBodegaFilterDto filtro, string impresoPor, CancellationToken ct = default);
}
