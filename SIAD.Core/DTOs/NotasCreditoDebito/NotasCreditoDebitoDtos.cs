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
