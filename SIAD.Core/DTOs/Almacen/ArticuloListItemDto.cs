namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Texto "id — descripción" para el buscador de artículos de movimientos de almacén.
    /// Usa el id interno a propósito, no el código SIMAFI (código legacy migrado).</summary>
    public string ComboTexto => $"{Id} — {Descripcion}";

    /// <summary>Texto libre legacy de la unidad (fallback si no hay catálogo enlazado).</summary>
    public string? UnidadMedida { get; init; }

    /// <summary>FK al catálogo de unidades (null si aún no migrada).</summary>
    public int? UnidadMedidaId { get; init; }

    /// <summary>Código de la unidad del catálogo, si está enlazada.</summary>
    public string? UnidadMedidaCodigo { get; init; }

    /// <summary>Descripción (nombre) de la unidad del catálogo, si está enlazada.</summary>
    public string? UnidadMedidaNombre { get; init; }

    /// <summary>Abreviatura de la unidad del catálogo, si está enlazada.</summary>
    public string? UnidadMedidaAbreviatura { get; init; }

    /// <summary>
    /// Etiqueta a mostrar: la DESCRIPCIÓN del catálogo, o vacío si el artículo no está
    /// enlazado. Sin fallback al texto libre legacy: en los migrados de SIMAFI ese campo
    /// trae diámetros ("18", "4X45"), no unidades, y mostrarlo era peor que no mostrar nada.
    /// (Hasta 2026-07-31 se mostraba el código del catálogo.)
    /// </summary>
    public string UnidadMedidaDisplay =>
        !string.IsNullOrWhiteSpace(UnidadMedidaNombre) ? UnidadMedidaNombre : string.Empty;

    /// <summary>
    /// Forma CORTA de la unidad, para acompañar cantidades (p. ej. "150.00 und").
    /// Abreviatura → código, SOLO del catálogo: el texto libre legacy de los migrados de
    /// SIMAFI trae diámetros y medidas ("18", "4X45"), no unidades, y pegado a la cantidad
    /// afirmaría algo falso. Vacío cuando el artículo no está enlazado al catálogo.
    /// </summary>
    public string UnidadMedidaCorta =>
        !string.IsNullOrWhiteSpace(UnidadMedidaAbreviatura) ? UnidadMedidaAbreviatura
        : (!string.IsNullOrWhiteSpace(UnidadMedidaCodigo) ? UnidadMedidaCodigo : string.Empty);

    /// <summary>Nombre del tipo de artículo (clasificación única desde la unificación 2026-07-16).</summary>
    public string? TipoArticuloNombre { get; init; }
    public string TipoArticuloDisplay => !string.IsNullOrWhiteSpace(TipoArticuloNombre) ? TipoArticuloNombre : "—";

    /// <summary>Nombre de la categoría (alm_grupo) del catálogo (fallback al código legacy).</summary>
    public string? Grupo { get; init; }
    public string? GrupoNombre { get; init; }
    public string GrupoDisplay => !string.IsNullOrWhiteSpace(GrupoNombre) ? GrupoNombre
        : (!string.IsNullOrWhiteSpace(Grupo) ? Grupo : "—");
    public string? Diametro { get; init; }
    public decimal Existencia { get; init; }

    /// <summary>
    /// Existencia del artículo EN la bodega por la que se filtró (<see cref="ArticuloFilterDto.BodegaId"/>).
    /// <c>null</c> cuando la consulta no filtró por bodega. Los movimientos que salen de UNA
    /// bodega (p. ej. el traslado) usan este stock real de la bodega de origen, no el rollup
    /// global <see cref="Existencia"/> (que suma todas las bodegas y sobreestimaría lo disponible).
    /// </summary>
    public decimal? ExistenciaEnBodega { get; init; }

    /// <summary>
    /// Costo promedio ponderado del artículo EN la bodega por la que se filtró
    /// (<see cref="ArticuloFilterDto.BodegaId"/>). <c>null</c> si no se filtró por bodega.
    /// Es el costo con el que el motor valora una SALIDA de esa bodega; la UI lo muestra
    /// como referencia (en salida el costo no se teclea, lo pone el sistema).
    /// </summary>
    public decimal? CostoPromedioEnBodega { get; init; }

    public decimal ExistenciaMinima { get; init; }
    public decimal ValorUnitario { get; init; }

    /// <summary>
    /// Valor de inventario en DINERO: existencia (rollup de cabecera) × valor unitario.
    /// Misma fórmula que <see cref="ArticulosResumenDto.ValorInventario"/>, así la suma de
    /// esta columna en el grid cuadra con la tarjeta KPI por construcción.
    /// (Hasta 2026-07-29 era una CANTIDAD: Σ existencias de ubicaciones.)
    /// </summary>
    public decimal ValorTotal { get; init; }

    /// <summary>Suma de la existencia de las ubicaciones ACTIVAS (para el detector de descuadre).</summary>
    public decimal ExistenciaBodegas { get; init; }

    /// <summary>true si la existencia de cabecera no cuadra con la suma de bodegas activas.</summary>
    public bool Descuadrado => Existencia != ExistenciaBodegas;

    /// <summary>true si hay mínimo definido y la existencia cayó por debajo.</summary>
    public bool BajoMinimo => ExistenciaMinima > 0 && Existencia < ExistenciaMinima;

    /// <summary>Soft-delete: false = artículo descontinuado (se conserva, no se ofrece para documentos nuevos).</summary>
    public bool Activo { get; init; } = true;
}
