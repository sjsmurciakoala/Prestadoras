using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Fila de la lista de movimientos de almacén. Trae ya resueltos los nombres de tipo y
/// bodega para que la grilla no tenga que cruzarlos.
/// </summary>
public sealed class MovimientoAlmacenListItemDto
{
    public int Id { get; init; }
    public int Numero { get; init; }
    public DateOnly Fecha { get; init; }

    public int TipoMovimientoId { get; init; }
    public string TipoCodigo { get; init; } = string.Empty;
    public string TipoNombre { get; init; } = string.Empty;

    /// <summary>ENTRADA · SALIDA · VALOR. La UI pinta el signo y el color con esto.</summary>
    public string Clase { get; init; } = string.Empty;

    public int BodegaId { get; init; }
    public string BodegaNombre { get; init; } = string.Empty;

    public string Motivo { get; init; } = string.Empty;
    public string? DocumentoReferencia { get; init; }
    public decimal Total { get; init; }
    public short Estado { get; init; }
    public int Renglones { get; init; }
    public string? UsuarioCreacion { get; init; }
}

/// <summary>Renglón del documento, tanto para capturar como para leer.</summary>
public sealed class MovimientoAlmacenDetalleDto
{
    public int? Id { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar un artículo.")]
    public int ArticuloId { get; set; }

    public string? CodigoArticulo { get; set; }

    /// <summary>Solo lectura: descripción del artículo, para la grilla.</summary>
    public string? NombreArticulo { get; set; }

    [Range(0.0001, double.MaxValue, ErrorMessage = "La cantidad debe ser mayor que cero.")]
    public decimal Cantidad { get; set; }

    /// <summary>Solo se teclea en clase ENTRADA. En SALIDA el motor usa el promedio vigente.</summary>
    public decimal CostoUnitario { get; set; }

    /// <summary>Solo lectura: costo con el que el motor valorizó realmente el asiento.</summary>
    public decimal? CostoReal { get; init; }

    public decimal Total { get; init; }
    public int? KardexId { get; init; }

    /// <summary>Solo lectura: existencia del par al momento de consultar, para orientar la captura.</summary>
    public decimal? ExistenciaActual { get; init; }

    /// <summary>Solo lectura: costo promedio vigente del par (bodega). Se muestra como referencia
    /// del costo con el que saldría en una SALIDA (donde el costo no se teclea).</summary>
    public decimal? CostoPromedioActual { get; init; }
}

/// <summary>Documento completo: cabecera + renglones.</summary>
public sealed class MovimientoAlmacenDto
{
    public int? Id { get; set; }
    public int Numero { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar el tipo de movimiento.")]
    public int TipoMovimientoId { get; set; }

    public string? TipoCodigo { get; init; }
    public string? TipoNombre { get; init; }
    public string? Clase { get; init; }

    [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar la bodega.")]
    public int BodegaId { get; set; }

    public string? BodegaNombre { get; init; }

    public DateOnly? Fecha { get; set; }

    [Required(ErrorMessage = "El motivo es obligatorio.")]
    [StringLength(120, ErrorMessage = "El motivo no puede superar los 120 caracteres.")]
    public string Motivo { get; set; } = string.Empty;

    [StringLength(30, ErrorMessage = "La referencia no puede superar los 30 caracteres.")]
    public string? DocumentoReferencia { get; set; }

    [StringLength(1000)]
    public string? Observaciones { get; set; }

    public decimal Total { get; init; }
    public short Estado { get; init; }
    public bool Posteado { get; init; }

    public string? AnuladoPor { get; init; }
    public DateTime? FechaAnulacion { get; init; }
    public string? MotivoAnulacion { get; init; }

    /// <summary>
    /// Idempotencia del documento: si se reenvía la misma petición con el mismo Uuid,
    /// el servicio devuelve el documento ya creado en vez de duplicarlo.
    /// </summary>
    public Guid? Uuid { get; set; }

    public string? UsuarioCreacion { get; init; }
    public DateTime? FechaCreacion { get; init; }

    public List<MovimientoAlmacenDetalleDto> Detalles { get; set; } = new();
}

/// <summary>Filtros de la lista. Todos opcionales; se combinan con AND.</summary>
public sealed class MovimientoAlmacenFilterDto
{
    public int? TipoMovimientoId { get; set; }
    public int? BodegaId { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public short? Estado { get; set; }

    /// <summary>Busca en motivo, referencia y número.</summary>
    public string? Search { get; set; }
}
