using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Documento de descargo (ENTREGA), Fase 6. Es la salida real: <b>sí postea</b> al kardex
/// (<c>SalidaDescargo</c>). Contra una requisición aprobada (entrega parcial) o directo con motivo.
/// Reutiliza la tabla plana <c>alm_descargo</c> como renglón / unidad de posteo.
/// </summary>
public sealed class DescargoDocumentoDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly? Fecha { get; set; }

    /// <summary>Requisición que se entrega. NULL = descargo directo (exige <see cref="Motivo"/>).</summary>
    public int? RequisicionHdrId { get; set; }
    public int? RequisicionNumero { get; set; }

    /// <summary>En modo contra-requisición se ignora: manda la bodega de la requisición.</summary>
    public int BodegaId { get; set; }
    public string? BodegaNombre { get; set; }

    public string? Departamento { get; set; }
    public string? EntregadoPor { get; set; }
    public string? RecibidoPor { get; set; }
    public string? Motivo { get; set; }
    public string? Observaciones { get; set; }

    public decimal Total { get; set; }
    public short Estado { get; set; }
    public bool Posteado { get; set; }
    public string? AnuladoPor { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }

    /// <summary>Idempotencia de la entrega. La UI lo genera y lo mantiene estable.</summary>
    public Guid? Uuid { get; set; }

    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }

    public List<DescargoDocumentoDetalleDto> Detalles { get; set; } = new();
}

public sealed class DescargoDocumentoDetalleDto
{
    public int Id { get; set; }

    /// <summary>LÍNEA de requisición servida (<c>alm_requisicion.id</c>). NULL en descargo directo.</summary>
    public int? RequisicionLineaId { get; set; }

    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? NombreArticulo { get; set; }
    public decimal Cantidad { get; set; }

    /// <summary>Costo con que salió (promedio vigente), lo estampa el motor al postear.</summary>
    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }

    /// <summary>Solo salida: cantidad aún pendiente de la línea de requisición (para orientar la entrega).</summary>
    public decimal? PendienteRequisicion { get; set; }
    public decimal? ExistenciaBodega { get; set; }
}

public sealed class DescargoDocumentoListItemDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public int? RequisicionHdrId { get; set; }
    public int? RequisicionNumero { get; set; }
    public int BodegaId { get; set; }
    public string? BodegaNombre { get; set; }
    public string? Departamento { get; set; }
    public decimal Total { get; set; }
    public short Estado { get; set; }
    public int Renglones { get; set; }
    public string? UsuarioCreacion { get; set; }
}

public sealed class DescargoDocumentoFilterDto
{
    public int? BodegaId { get; set; }
    public short? Estado { get; set; }
    public int? RequisicionHdrId { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public string? Search { get; set; }
}
