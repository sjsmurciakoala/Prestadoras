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
    public string? Linea { get; init; }
    public string? Grupo { get; init; }

    /// <summary>Nombre de la clasificación por uso (si está asignada).</summary>
    public string? TipoArticuloNombre { get; init; }

    /// <summary>Nombre de la línea del catálogo (fallback al código legacy).</summary>
    public string? LineaNombre { get; init; }
    public string LineaDisplay => !string.IsNullOrWhiteSpace(LineaNombre) ? LineaNombre
        : (!string.IsNullOrWhiteSpace(Linea) ? Linea : "—");

    /// <summary>Nombre del grupo del catálogo (fallback al código legacy).</summary>
    public string? GrupoNombre { get; init; }
    public string GrupoDisplay => !string.IsNullOrWhiteSpace(GrupoNombre) ? GrupoNombre
        : (!string.IsNullOrWhiteSpace(Grupo) ? Grupo : "—");
    public string? Diametro { get; init; }
    public string? CuentaContable { get; init; }
    public decimal Existencia { get; init; }
    public decimal ExistenciaMinima { get; init; }
    public decimal ValorUnitario { get; init; }

    /// <summary>Existencia × valor unitario (valorización de la línea).</summary>
    public decimal ValorTotal => Existencia * ValorUnitario;

    /// <summary>true si hay mínimo definido y la existencia cayó por debajo.</summary>
    public bool BajoMinimo => ExistenciaMinima > 0 && Existencia < ExistenciaMinima;
}
