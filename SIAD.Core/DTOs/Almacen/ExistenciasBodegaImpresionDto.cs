using System.Collections.Generic;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Datos de impresión del reporte "Existencias por bodega (valorado)": el encabezado de la
/// empresa (heredado) + la lista de existencias a listar (agrupada por bodega en el reporte) +
/// el texto de los filtros aplicados. A diferencia de los comprobantes, no es una transacción
/// sino un listado; por eso lleva la colección completa de líneas.
/// </summary>
public sealed class ExistenciasBodegaImpresionDto : ComprobanteAlmacenImpresionBase
{
    /// <summary>Título del reporte. Se parametriza para reutilizar el mismo reporte en la
    /// valuación a una fecha ("VALUACIÓN DE INVENTARIO AL ...").</summary>
    public string Titulo { get; set; } = "EXISTENCIAS POR BODEGA (VALORADO)";

    /// <summary>Líneas del reporte (una por artículo/bodega), ya ordenadas por bodega y artículo.</summary>
    public List<ExistenciaBodegaItemDto> Items { get; set; } = new();

    /// <summary>Descripción legible de los filtros aplicados (para el encabezado del reporte).</summary>
    public string FiltroTexto { get; set; } = string.Empty;
}
