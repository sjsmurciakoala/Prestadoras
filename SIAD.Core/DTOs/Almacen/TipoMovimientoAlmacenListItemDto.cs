namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Fila del catálogo de tipos de movimiento de almacén (<c>alm_tipo_movimiento</c>) para la
/// grilla de mantenimiento.
/// <para>
/// Distinto de <see cref="TipoMovimientoDto"/>, que es el lookup de los códigos legacy de
/// transacción del kardex (102/202/103) usado por el filtro de KardexArticulo. Éste es el
/// catálogo de negocio configurable.
/// </para>
/// </summary>
public sealed class TipoMovimientoAlmacenListItemDto
{
    public int Id { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;

    /// <summary>ENTRADA · SALIDA · VALOR (ver <see cref="SIAD.Core.Constants.ClaseAjusteInventario"/>).</summary>
    public string Clase { get; init; } = string.Empty;

    public bool RequiereAutorizacion { get; init; }
    public bool Activo { get; init; }

    /// <summary>
    /// true = el tipo ya tiene movimientos posteados, así que su <see cref="Clase"/> no se
    /// puede cambiar. En esta fase (sin documentos de movimiento) siempre es false; la Fase 2
    /// lo deriva de <c>alm_movimiento_dtl</c>.
    /// </summary>
    public bool EnUso { get; init; }
}
