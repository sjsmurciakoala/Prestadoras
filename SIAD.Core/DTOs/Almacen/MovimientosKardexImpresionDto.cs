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

    /// <summary>Cantidad con signo (entradas +, salidas −): la columna combinada del estado de cuenta.</summary>
    public decimal CantidadFirmada { get; set; }

    /// <summary>Saldo corrido (kardex por artículo) o existencia resultante (libro de bodega). Null = "—".</summary>
    public decimal? Saldo { get; set; }

    /// <summary>Saldo valorizado corrido (sólo el estado de cuenta por artículo). Null = "—".</summary>
    public decimal? SaldoValorizado { get; set; }

    /// <summary>
    /// Costo promedio DESPUÉS del asiento: derivado del libro en el kardex por artículo,
    /// snapshot del motor en el libro de bodega. Null = "—" (pre-corte o sin saldo positivo).
    /// </summary>
    public decimal? CostoPromedio { get; set; }

    /// <summary>Valor monetario del movimiento, con signo: (entradas − salidas) × valor unitario.</summary>
    public decimal ValorMovimiento { get; set; }
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

    /// <summary>
    /// Diseño de columnas del estado de cuenta por artículo: una sola columna de cantidad con
    /// signo, el concepto fundido en la descripción, sin Doc./Tipo/Contexto y el costo promedio
    /// al final. En false (libro por bodega) se conserva el diseño tabular clásico.
    /// </summary>
    public bool ModoEstadoCuenta { get; set; }

    public List<MovimientoKardexImpresionRow> Filas { get; set; } = new();

    public decimal TotalEntradas { get; set; }
    public decimal TotalSalidas { get; set; }

    // ── Valorización ─────────────────────────────────────────────────────────

    /// <summary>Valor monetario de las entradas del período.</summary>
    public decimal ValorEntradas { get; set; }

    /// <summary>Valor monetario de las salidas del período.</summary>
    public decimal ValorSalidas { get; set; }

    /// <summary>Valor del inventario al cierre.</summary>
    public decimal SaldoValorizado { get; set; }

    /// <summary>Costo promedio al cierre. Null = sin saldo positivo.</summary>
    public decimal? CostoPromedioFinal { get; set; }

    /// <summary>
    /// Si el cierre valorizado tiene sentido en este reporte. En el kardex por artículo sí: hay
    /// un saldo y un costo del par. En el libro de bodega no: la página mezcla artículos, así que
    /// sumar sus saldos daría un número sin significado y sólo se imprimen los valores movidos.
    /// </summary>
    public bool MuestraSaldoValorizado { get; set; }

    // ── Arrastre (el "saldo anterior" del kardex legacy) ─────────────────────
    // Sólo se llena cuando la consulta tuvo fecha desde y había historia previa.

    public decimal? CantidadAnterior { get; set; }
    public decimal? ValorAnterior { get; set; }
    public decimal? CostoPromedioAnterior { get; set; }

    /// <summary>true si hay un arrastre que imprimir en el encabezado.</summary>
    public bool TieneArrastre => CantidadAnterior.HasValue;
}
