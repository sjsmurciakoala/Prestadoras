namespace SIAD.Core.DTOs.Almacen;

public sealed class ArticuloListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Descripcion { get; init; } = string.Empty;

    /// <summary>Texto libre legacy de la unidad (fallback si no hay catálogo enlazado).</summary>
    public string? UnidadMedida { get; init; }

    /// <summary>FK al catálogo de unidades (null si aún no migrada).</summary>
    public int? UnidadMedidaId { get; init; }

    /// <summary>Código de la unidad del catálogo, si está enlazada.</summary>
    public string? UnidadMedidaCodigo { get; init; }

    /// <summary>Etiqueta a mostrar: catálogo si está enlazado, si no el texto libre.</summary>
    public string UnidadMedidaDisplay =>
        !string.IsNullOrWhiteSpace(UnidadMedidaCodigo) ? UnidadMedidaCodigo
        : (!string.IsNullOrWhiteSpace(UnidadMedida) ? UnidadMedida : "—");

    /// <summary>Nombre del tipo de artículo (clasificación única desde la unificación 2026-07-16).</summary>
    public string? TipoArticuloNombre { get; init; }
    public string TipoArticuloDisplay => !string.IsNullOrWhiteSpace(TipoArticuloNombre) ? TipoArticuloNombre : "—";

    /// <summary>Nombre de la categoría (alm_grupo) del catálogo (fallback al código legacy).</summary>
    public string? Grupo { get; init; }
    public string? GrupoNombre { get; init; }
    public string GrupoDisplay => !string.IsNullOrWhiteSpace(GrupoNombre) ? GrupoNombre
        : (!string.IsNullOrWhiteSpace(Grupo) ? Grupo : "—");
    public string? Diametro { get; init; }
    public string? CuentaContable { get; init; }
    public decimal Existencia { get; init; }
    public decimal ExistenciaMinima { get; init; }
    public decimal ValorUnitario { get; init; }

    /// <summary>Suma de la existencia de todas las ubicaciones (bodegas) del artículo.</summary>
    public decimal ValorTotal { get; init; }

    /// <summary>true si hay mínimo definido y la existencia cayó por debajo.</summary>
    public bool BajoMinimo => ExistenciaMinima > 0 && Existencia < ExistenciaMinima;
}
