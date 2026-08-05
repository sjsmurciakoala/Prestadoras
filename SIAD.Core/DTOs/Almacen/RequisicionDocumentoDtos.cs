using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Documento de requisición (cabecera + renglones), Fase 6. Es la SOLICITUD: nunca mueve
/// inventario — la salida la produce el descargo. Reutiliza la tabla plana <c>alm_requisicion</c>
/// como renglón (con <c>requisicion_hdr_id</c>), igual que compras.
/// </summary>
public sealed class RequisicionDocumentoDto
{
    public int Id { get; set; }
    public int Numero { get; set; }

    /// <summary>1 Salida de bodega · 2 Reabastecimiento (fuera de alcance 1ª entrega).</summary>
    public short Tipo { get; set; } = 1;

    /// <summary>Ver <c>EstadoRequisicionHdr</c>.</summary>
    public short Estado { get; set; }

    public DateOnly? Fecha { get; set; }
    public DateOnly? FechaRequerida { get; set; }

    public int BodegaId { get; set; }
    public string? BodegaNombre { get; set; }

    public string? Departamento { get; set; }
    public string? Solicitante { get; set; }
    public string? CargoSolicitante { get; set; }
    public string? Aplicacion { get; set; }
    public string? Observacion { get; set; }

    public string? AprobadoPor { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public string? RechazadoPor { get; set; }
    public DateTime? FechaRechazo { get; set; }
    public string? MotivoRechazo { get; set; }
    public string? AnuladoPor { get; set; }
    public DateTime? FechaAnulacion { get; set; }
    public string? MotivoAnulacion { get; set; }

    public decimal Total { get; set; }

    /// <summary>Idempotencia del alta. La UI lo genera y lo mantiene estable entre reintentos.</summary>
    public Guid? Uuid { get; set; }

    public string? UsuarioCreacion { get; set; }
    public DateTime? FechaCreacion { get; set; }

    public List<RequisicionDocumentoDetalleDto> Detalles { get; set; } = new();
}

public sealed class RequisicionDocumentoDetalleDto
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? NombreArticulo { get; set; }
    public string? Descripcion { get; set; }
    public decimal Cantidad { get; set; }

    /// <summary>Cantidad ya entregada por descargos. Pendiente = Cantidad − CantidadDespachada.</summary>
    public decimal CantidadDespachada { get; set; }

    public decimal PrecioUnitario { get; set; }
    public decimal Total { get; set; }
    public string? CuentaContable { get; set; }

    /// <summary>Existencia actual del par (artículo, bodega de la requisición), para orientar al capturar.</summary>
    public decimal? ExistenciaBodega { get; set; }
}

public sealed class RequisicionDocumentoListItemDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public short Tipo { get; set; }
    public short Estado { get; set; }
    public int BodegaId { get; set; }
    public string? BodegaNombre { get; set; }
    public string? Departamento { get; set; }
    public string? Solicitante { get; set; }
    public decimal Total { get; set; }
    public int Renglones { get; set; }
    /// <summary>% despachado = Σ despachada / Σ solicitada (0–100).</summary>
    public decimal PorcentajeDespachado { get; set; }
    public string? UsuarioCreacion { get; set; }
}

public sealed class RequisicionDocumentoFilterDto
{
    public int? BodegaId { get; set; }
    public short? Estado { get; set; }
    public short? Tipo { get; set; }
    public DateOnly? Desde { get; set; }
    public DateOnly? Hasta { get; set; }
    public string? Search { get; set; }
}

/// <summary>Cuerpo para rechazar o anular (motivo obligatorio).</summary>
public sealed class MotivoRequisicionDto
{
    public string? Motivo { get; set; }
}
