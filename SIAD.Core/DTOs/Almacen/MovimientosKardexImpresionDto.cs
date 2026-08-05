using System.Collections.Generic;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>Una fila del reporte imprimible de movimientos de kardex (ya formateada para el reporte).</summary>
public sealed class MovimientoKardexImpresionRow
{
    /// <summary>Fecha ya formateada (dd/MM/yyyy) o "—".</summary>
    public string Fecha { get; set; } = string.Empty;

    /// <summary>Número de documento ya formateado o "—".</summary>
    public string Documento { get; set; } = string.Empty;

    /// <summary>Tipo de movimiento legible.</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Columna de contexto: la bodega (en el kardex por artículo) o el artículo (en el libro de bodega).</summary>
    public string Contexto { get; set; } = string.Empty;

    /// <summary>Descripción del asiento o del artículo.</summary>
    public string Descripcion { get; set; } = string.Empty;

    public decimal Entradas { get; set; }
    public decimal Salidas { get; set; }
    public decimal ValorUnitario { get; set; }

    /// <summary>Saldo corrido (kardex por artículo) o existencia resultante (libro de bodega). Null = "—".</summary>
    public decimal? Saldo { get; set; }
}

/// <summary>
/// Datos de impresión del reporte de movimientos de kardex. Un mismo reporte
/// (<c>Rpt_Dev_Movimientos_Kardex</c>) sirve para el kardex de un artículo y para el libro de
/// movimientos de una bodega; cambian el título, el subtítulo y el nombre de la columna de contexto.
/// </summary>
public sealed class MovimientosKardexImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string Titulo { get; set; } = "MOVIMIENTOS DE KARDEX";

    /// <summary>Segunda línea: el artículo o la bodega consultada.</summary>
    public string Subtitulo { get; set; } = string.Empty;

    /// <summary>Descripción legible de los filtros aplicados (período, etc.).</summary>
    public string FiltroTexto { get; set; } = string.Empty;

    /// <summary>Encabezado de la columna de contexto ("Bodega" o "Artículo").</summary>
    public string ColumnaContexto { get; set; } = "Bodega";

    public List<MovimientoKardexImpresionRow> Filas { get; set; } = new();

    public decimal TotalEntradas { get; set; }
    public decimal TotalSalidas { get; set; }
}
