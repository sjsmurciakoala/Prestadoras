using System.ComponentModel.DataAnnotations;

namespace SIAD.Core.DTOs.NotasCreditoDebito;

// =============================================================================
// Modelo NC/ND V3 (Sprint 3, 2026-05-14) — conforme SAR Acuerdo 481-2017.
// Reemplaza el modelo legacy que escribía en `ajustes` / `transaccion_abonado`.
// =============================================================================

/// <summary>Cliente para búsqueda inicial.</summary>
public class NotaClienteLookupDto
{
    public string Clave { get; set; } = string.Empty;
    public string Nombre { get; set; } = string.Empty;
    public string? Direccion { get; set; }
    public string? Rtn { get; set; }
    public string? Categoria { get; set; }
    public string? CicloCodigo { get; set; }
    public string? CicloDescripcion { get; set; }
}

/// <summary>Factura del cliente candidata a recibir una NC/ND.</summary>
public class FacturaOrigenLookupDto
{
    public int FacturaId { get; set; }
    public string NumeroFactura { get; set; } = string.Empty;
    public DateTime? FechaEmision { get; set; }
    public string? Periodo { get; set; }
    public decimal SaldoTotal { get; set; }
    public string? Estado { get; set; }
}

/// <summary>Motivo de anulación (NC) o de aumento (ND).</summary>
public class MotivoLookupDto
{
    public short Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
}

/// <summary>CAI disponible para emitir NC (tipo 6) o ND (tipo 7).</summary>
public class CaiNotaLookupDto
{
    public long CaiId { get; set; }
    public string CodigoCai { get; set; } = string.Empty;
    public string PrefijoDocumento { get; set; } = string.Empty;
    public long CorrelativoActual { get; set; }
    public long RangoHasta { get; set; }
    public short TipoDocumentoFiscalId { get; set; }
    public long SiguienteCorrelativo => CorrelativoActual + 1;
}

/// <summary>Request para emitir una Nota de Crédito.</summary>
public class EmitirNotaCreditoRequestDto
{
    [Required]
    public int FacturaOrigenId { get; set; }

    [Required]
    public short MotivoAnulacionId { get; set; }

    public string? MotivoDetalle { get; set; }

    /// <summary>NULL = disminuir el total de la factura origen (anula).</summary>
    public decimal? MontoDisminuir { get; set; }

    [Required]
    public long CaiId { get; set; }

    public string Usuario { get; set; } = string.Empty;
}

/// <summary>Request para emitir una Nota de Débito.</summary>
public class EmitirNotaDebitoRequestDto
{
    [Required]
    public int FacturaOrigenId { get; set; }

    [Required]
    public short MotivoAumentoId { get; set; }

    public string? MotivoDetalle { get; set; }

    [Range(typeof(decimal), "0.01", "79228162514264337593543950335")]
    public decimal MontoAumentar { get; set; }

    [Required]
    public long CaiId { get; set; }

    public string Usuario { get; set; } = string.Empty;
}

/// <summary>Resultado de emitir una NC/ND.</summary>
public class EmitirNotaResponseDto
{
    public bool Success { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public long NotaId { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public long Correlativo { get; set; }
}

/// <summary>Fila del listado de notas emitidas (sirve para NC y ND).</summary>
public class NotaEmitidaListDto
{
    public long NotaId { get; set; }
    public string TipoNota { get; set; } = string.Empty;  // "NC" | "ND"
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public long ClienteId { get; set; }
    /// <summary>Número de cuenta (clave) del cliente — pruebas operativas jul-2026.</summary>
    public string ClienteClave { get; set; } = string.Empty;
    public string ClienteNombre { get; set; } = string.Empty;
    public string FacturaOrigenNumero { get; set; } = string.Empty;
    public short MotivoId { get; set; }
    public string MotivoDescripcion { get; set; } = string.Empty;
    public string? MotivoDetalle { get; set; }
    public decimal Monto { get; set; }            // monto_disminuir o monto_aumentar
    public decimal TotalNota { get; set; }
    public short EstadoId { get; set; }
    public string EstadoDescripcion { get; set; } = string.Empty;
    public bool AnulaFacturaOrigen { get; set; }  // solo aplica a NC
    public string UsuarioEmisor { get; set; } = string.Empty;
}

/// <summary>Filtros para el listado server-side de notas emitidas.</summary>
public class NotaEmitidaFilterDto
{
    public string? Search { get; set; }
    public string? TipoNota { get; set; }   // "NC" | "ND" | null (ambas)
    public short? EstadoId { get; set; }
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
}

// ── Impresión / vista previa (pruebas operativas jul-2026) ──

/// <summary>Línea de detalle para el documento impreso de una NC/ND.</summary>
public class NotaImpresionLineaDto
{
    public string Descripcion { get; set; } = string.Empty;
    public decimal Cantidad { get; set; }
    public decimal MontoUnitario { get; set; }
    public decimal MontoTotal { get; set; }
}

/// <summary>
/// Datos completos para imprimir (o previsualizar) una NC/ND. Todo lo fiscal
/// viaja tal como quedó grabado en la nota (emisor, receptor, CAI, leyenda).
/// </summary>
public class NotaImpresionDto
{
    public string TipoNota { get; set; } = string.Empty;          // "NC" | "ND"
    public string TituloDocumento { get; set; } = string.Empty;   // "NOTA DE CRÉDITO" | "NOTA DE DÉBITO"
    public string NumeroDocumento { get; set; } = string.Empty;
    public DateTime FechaEmision { get; set; }
    public string? CodigoCai { get; set; }
    public string? LeyendaCaiRango { get; set; }
    public DateTime? FechaLimiteCai { get; set; }

    public string EmisorNombre { get; set; } = string.Empty;
    public string EmisorRtn { get; set; } = string.Empty;
    public string? EmisorDireccion { get; set; }

    /// <summary>Número de cuenta (clave) del cliente — pedido explícito del equipo.</summary>
    public string ClienteClave { get; set; } = string.Empty;
    public string ReceptorNombre { get; set; } = string.Empty;
    public string? ReceptorRtn { get; set; }
    public string? ReceptorDireccion { get; set; }

    public string FacturaOrigenNumero { get; set; } = string.Empty;
    public DateTime? FacturaOrigenFecha { get; set; }
    public string? FacturaOrigenCai { get; set; }

    public string MotivoDescripcion { get; set; } = string.Empty;
    public string? MotivoDetalle { get; set; }

    public List<NotaImpresionLineaDto> Lineas { get; set; } = new();
    public decimal SubTotal { get; set; }
    public decimal Isv { get; set; }
    public decimal Total { get; set; }

    public bool AnulaFacturaOrigen { get; set; }
    public string UsuarioEmisor { get; set; } = string.Empty;

    /// <summary>true = generado SIN guardar (marca de agua, sin valor fiscal).</summary>
    public bool EsVistaPrevia { get; set; }
}

// ── Mantenimiento de catálogos de motivos ──

/// <summary>Fila del CRUD de motivos (NC: anulación, ND: aumento).</summary>
public class MotivoCrudDto
{
    public short Id { get; set; }
    public string Codigo { get; set; } = string.Empty;
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; }
}

/// <summary>Request para crear/actualizar un motivo.</summary>
public class MotivoSaveRequestDto
{
    public short? Id { get; set; }   // null = nuevo
    [Required]
    public string Codigo { get; set; } = string.Empty;
    [Required]
    public string Descripcion { get; set; } = string.Empty;
    public bool Activo { get; set; } = true;
}
