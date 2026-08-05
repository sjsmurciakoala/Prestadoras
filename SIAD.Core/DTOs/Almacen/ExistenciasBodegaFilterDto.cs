namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Filtro del reporte "Existencias por bodega (valorado)": una fila por (artículo, bodega)
/// con existencia y valor a costo promedio, agrupable por bodega. Fuente: alm_articulo_bodega.
/// </summary>
public sealed class ExistenciasBodegaFilterDto
{
    /// <summary>Acota a una bodega. Null = todas las bodegas.</summary>
    public int? BodegaId { get; set; }

    /// <summary>Acota a un tipo de artículo. Null = todos.</summary>
    public int? TipoArticuloId { get; set; }

    /// <summary>
    /// false (defecto) = sólo filas con existencia distinta de cero (lo típico de un reporte
    /// de existencias); true = incluye también las ubicaciones activas con existencia en 0.
    /// </summary>
    public bool IncluirSinExistencia { get; set; }

    /// <summary>Busca por código o descripción del artículo. Null/vacío = todos.</summary>
    public string? Search { get; set; }
}
