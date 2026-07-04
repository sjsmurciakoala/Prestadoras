using System;

namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// Línea agregada del preview del lote de partidas de facturación
/// (fn_con_preview_partidas_facturacion, plan 2026-07-02 F3).
/// </summary>
public sealed class LotePreviewLineaDto
{
    public DateOnly FechaPartida { get; set; }
    public string Uso { get; set; } = string.Empty;
    public long AccountId { get; set; }
    public string AccountCode { get; set; } = string.Empty;
    public string AccountName { get; set; } = string.Empty;
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public long Facturas { get; set; }
}

/// <summary>Petición de generación de lote (body del POST generar).</summary>
public sealed class LoteGenerarRequestDto
{
    public DateOnly Desde { get; set; }
    public DateOnly Hasta { get; set; }
    public string Modo { get; set; } = "DIA";
}

/// <summary>
/// Resultado de sp_con_generar_partidas_facturacion.
/// <see cref="LoteId"/> es null cuando no había facturas pendientes en el rango.
/// </summary>
public sealed class LoteGenerarResultDto
{
    public long? LoteId { get; set; }
    public int Polizas { get; set; }
    public int Facturas { get; set; }
    public int Encoladas { get; set; }
    public decimal Total { get; set; }
}

/// <summary>
/// Fila del historial de lotes (con_lote_facturacion).
/// Estados: 1=Generado, 2=Parcial, 3=Encolado.
/// </summary>
public sealed class LoteFacturacionDto
{
    public long LoteId { get; set; }
    public DateOnly FechaDesde { get; set; }
    public DateOnly FechaHasta { get; set; }
    public string ModoAgrupacion { get; set; } = "DIA";
    public int Facturas { get; set; }
    public int Polizas { get; set; }
    public int Encoladas { get; set; }
    public decimal Total { get; set; }
    public short StatusId { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

/// <summary>
/// Partida encolada en la cola de regularización (con_partida_pendiente,
/// módulo VENTAS, estado 1=PENDIENTE) a la espera de reproceso.
/// </summary>
public sealed class PartidaPendienteDto
{
    public long PartidaPendienteId { get; set; }
    public DateOnly FechaDocumento { get; set; }
    public string? Descripcion { get; set; }
    public string Motivo { get; set; } = string.Empty;
    public short StatusId { get; set; }
    public int Intentos { get; set; }
    public DateTime CreatedAt { get; set; }

    // Rango y modo originales del payload (para reprocesar el lote COMPLETO
    // que quedó encolado, no solo el día de fecha_documento).
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? ModoAgrupacion { get; set; }
}
