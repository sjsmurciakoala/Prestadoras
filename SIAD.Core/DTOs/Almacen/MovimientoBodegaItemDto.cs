using System;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Una línea del libro de movimientos de bodega (un asiento de <c>alm_kardex</c> de la
/// bodega consultada). Incluye el artículo del asiento, porque el libro mezcla todos los
/// artículos de la bodega.
/// </summary>
public sealed class MovimientoBodegaItemDto
{
    public int Id { get; init; }
    public DateOnly? Fecha { get; init; }
    public decimal? NumeroDocumento { get; init; }
    public string? TipoTransaccion { get; init; }
    public string TipoDescripcion => TipoMovimientoKardex.Describir(TipoTransaccion);

    public int? ArticuloId { get; init; }

    /// <summary>Código del artículo (del catálogo o, en histórico, el código SIMAFI snapshot).</summary>
    public string? ArticuloCodigo { get; init; }

    /// <summary>Descripción del artículo del catálogo (null en histórico sin re-enlazar).</summary>
    public string? ArticuloDescripcion { get; init; }

    /// <summary>Descripción/documento del asiento (alm_kardex.descripcion).</summary>
    public string? Descripcion { get; init; }

    public decimal Ingresos { get; init; }
    public decimal Salidas { get; init; }
    public decimal ValorUnitario { get; init; }
    public decimal Total { get; init; }

    /// <summary>
    /// Existencia del par (artículo, bodega) DESPUÉS de este asiento (snapshot del motor).
    /// Null en el histórico SIMAFI, que no fue posteado por el motor.
    /// </summary>
    public decimal? ExistenciaResultante { get; init; }

    /// <summary>Costo promedio del par (artículo, bodega) DESPUÉS de este asiento.</summary>
    public decimal? CostoPromedioResultante { get; init; }

    /// <summary>Tipo del documento origen (<c>TipoDocumentoInventario</c>). Null = histórico SIMAFI.</summary>
    public string? DocumentoTipo { get; init; }
    public int? DocumentoId { get; init; }

    public string? UsuarioCreacion { get; init; }
    public DateTime? FechaCreacion { get; init; }
}
