using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Documento de traslado entre bodegas. Ver
/// <c>docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md</c>.
/// Reutiliza <c>alm_movimiento_hdr/_dtl</c> (clase TRASLADO) + las tablas de recepción.
/// </summary>
public sealed class TrasladoDto
{
    public int Id { get; set; }
    public int Numero { get; set; }

    public int TipoMovimientoId { get; set; }
    public string? TipoCodigo { get; set; }
    public string? TipoNombre { get; set; }

    public int BodegaOrigenId { get; set; }
    public string? BodegaOrigenNombre { get; set; }
    public int BodegaDestinoId { get; set; }
    public string? BodegaDestinoNombre { get; set; }

    /// <summary>true = con recepción (dos pasos) · false = directo (un paso).</summary>
    public bool RequiereRecepcion { get; set; } = true;

    public DateOnly? Fecha { get; set; }
    public string? Motivo { get; set; }
    public string? DocumentoReferencia { get; set; }
    public string? Observaciones { get; set; }

    public decimal Total { get; set; }
    public short Estado { get; set; }

    public string? RecibidoPor { get; set; }
    public DateTime? FechaRecepcion { get; set; }
    public string? AnuladoPor { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }

    /// <summary>Idempotencia del ENVÍO. La UI lo genera y lo mantiene estable entre reintentos.</summary>
    public Guid? Uuid { get; set; }

    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }

    public List<TrasladoDetalleDto> Detalles { get; set; } = new();
    public List<TrasladoRecepcionDto> Recepciones { get; set; } = new();
}

public sealed class TrasladoDetalleDto
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? NombreArticulo { get; set; }

    /// <summary>Cantidad enviada (salió de origen).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Cantidad ya recibida en destino (acumulada). Pendiente = Cantidad − CantidadRecibida.</summary>
    public decimal CantidadRecibida { get; set; }

    /// <summary>Costo con que viajó (promedio de origen al enviar).</summary>
    public decimal? CostoReal { get; set; }
    public decimal Total { get; set; }

    public decimal? ExistenciaOrigen { get; set; }
    public decimal? ExistenciaDestino { get; set; }
    public int? KardexId { get; set; }
}

public sealed class TrasladoListItemDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public int BodegaOrigenId { get; set; }
    public string? BodegaOrigenNombre { get; set; }
    public int BodegaDestinoId { get; set; }
    public string? BodegaDestinoNombre { get; set; }
    public bool RequiereRecepcion { get; set; }
    public string? Motivo { get; set; }
    public decimal Total { get; set; }
    public short Estado { get; set; }
    public int Renglones { get; set; }
    /// <summary>% recibido = Σ recibida / Σ enviada (0–100). 100 en un directo.</summary>
    public decimal PorcentajeRecibido { get; set; }
    public string? UsuarioCreacion { get; set; }
}

public sealed class TrasladoFilterDto
{
    public int? BodegaOrigenId { get; set; }
    public int? BodegaDestinoId { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public short? Estado { get; set; }
    public string? Search { get; set; }
}

/// <summary>Una tanda de recepción de un traslado (recepción parcial).</summary>
public sealed class RecepcionTrasladoDto
{
    /// <summary>Idempotencia del ACTO de recepción. La UI lo genera y lo mantiene estable.</summary>
    public Guid? Uuid { get; set; }
    public DateOnly? Fecha { get; set; }
    public string? Observaciones { get; set; }
    public List<RecepcionTrasladoLineaDto> Lineas { get; set; } = new();
}

public sealed class RecepcionTrasladoLineaDto
{
    /// <summary>Renglón del traslado que se recibe (<c>alm_movimiento_dtl.id</c>).</summary>
    public int MovimientoDtlId { get; set; }
    public decimal Cantidad { get; set; }
}

/// <summary>Recepción ya registrada, para mostrar en el detalle del traslado.</summary>
public sealed class TrasladoRecepcionDto
{
    public int Id { get; set; }
    public DateOnly Fecha { get; set; }
    public string? Observaciones { get; set; }
    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }
    public List<TrasladoRecepcionLineaDto> Lineas { get; set; } = new();
}

public sealed class TrasladoRecepcionLineaDto
{
    public int Id { get; set; }
    public int MovimientoDtlId { get; set; }
    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? NombreArticulo { get; set; }
    public decimal Cantidad { get; set; }
    public decimal CostoReal { get; set; }
    public decimal Total { get; set; }
    public int? KardexId { get; set; }
}
