using SIAD.Core.DTOs.Almacen;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Datos de la <b>ficha de evaluación</b> impresa: una hoja por proveedor con el desglose que
/// sustenta la calificación y espacio para firmas. Es el documento que se le muestra al proveedor
/// o se archiva como respaldo de la clasificación.
/// </summary>
public sealed class EvaluacionFichaImpresionDto : ComprobanteAlmacenImpresionBase
{
    public EvaluacionFichaDto Ficha { get; set; } = new();

    /// <summary>
    /// Nota al pie sobre los criterios sin datos. Va en el documento porque explica por qué el
    /// «peso aplicado» de cada criterio no coincide con el peso configurado.
    /// </summary>
    public string? NotaCriteriosSinDatos { get; set; }
}

/// <summary>
/// Datos del <b>cuadro comparativo</b>: un renglón por proveedor evaluado en el período, con el
/// logro de cada criterio y la calificación final. Es el listado que revisa gerencia.
/// </summary>
public sealed class EvaluacionComparativoImpresionDto : ComprobanteAlmacenImpresionBase
{
    public string PeriodoCodigo { get; set; } = string.Empty;
    public string PeriodoNombre { get; set; } = string.Empty;
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    public bool PeriodoCerrado { get; set; }

    /// <summary>Criterios del catálogo, en orden: definen las columnas del cuadro.</summary>
    public List<EvaluacionCriterioDto> Criterios { get; set; } = new();

    public List<EvaluacionRankingItemDto> Items { get; set; } = new();

    public decimal? PromedioPuntaje { get; set; }
    public int Evaluados => Items.Count;

    /// <summary>Texto del filtro aplicado, para que el papel diga qué se está viendo.</summary>
    public string? FiltroTexto { get; set; }
}
