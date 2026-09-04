using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;

namespace SIAD.Core.DTOs.Proveedores;

/// <summary>
/// Filtro de la vista unificada de cuentas por pagar (facturas de compra + compromisos).
/// Mapea a los parámetros de <c>fn_prv_cxp_documentos</c>.
/// </summary>
public sealed class CxpUnificadaFilterDto
{
    /// <summary>Proveedor, código, número de documento o concepto.</summary>
    [StringLength(150, ErrorMessage = "La búsqueda no puede superar 150 caracteres.")]
    public string? Search { get; set; }

    /// <summary>Ver <see cref="OrigenDocumentoProveedor"/>: 1 compra, 2 compromiso. Null = ambos.</summary>
    public short? Origen { get; set; }

    /// <summary>Escala de <see cref="EstadoCompraCxp"/>: 1 Pendiente, 2 Parcial, 3 Pagada.</summary>
    public short? EstadoId { get; set; }

    [StringLength(20, ErrorMessage = "El código del proveedor no puede superar 20 caracteres.")]
    public string? CodProveedor { get; set; }

    /// <summary>Solo los que conservan saldo y ya pasaron su fecha de vencimiento.</summary>
    public bool SoloVencidos { get; set; }

    /// <summary>
    /// La pantalla arranca con lo pendiente (D2 del plan); los saldados solo aparecen
    /// cuando se activa este filtro.
    /// </summary>
    public bool IncluirPagados { get; set; }
}

/// <summary>
/// Una fila de la vista unificada: un documento por pagar, venga de compras o de compromisos.
/// Mapea 1:1 con <c>fn_prv_cxp_documentos</c>.
/// </summary>
public sealed class CxpDocumentoDto
{
    /// <summary>Módulo del que nace: ver <see cref="OrigenDocumentoProveedor"/>.</summary>
    public short Origen { get; set; }

    /// <summary>Id dentro de su módulo: <c>alm_compra_cxp.id</c> o <c>prv_compromiso_hdr.numero_orden</c>.</summary>
    public long DocumentoId { get; set; }

    /// <summary>Número visible: factura SAR (o <c>FAC-000123</c>) y <c>OPD-00412</c>.</summary>
    public string NumeroDocumento { get; set; } = string.Empty;

    public string CodProveedor { get; set; } = string.Empty;
    public string Proveedor { get; set; } = string.Empty;

    public DateOnly Fecha { get; set; }

    /// <summary>
    /// Null cuando el documento no tiene plazo propio: es el caso del compromiso (D1 del plan).
    /// En pantalla se muestra «sin plazo», nunca una fecha inventada.
    /// </summary>
    public DateOnly? FechaVencimiento { get; set; }

    public string Concepto { get; set; } = string.Empty;

    public decimal Monto { get; set; }
    public decimal Abonado { get; set; }
    public decimal Saldo { get; set; }

    /// <summary>Días desde el vencimiento (negativo = aún no vence). Null si el documento no tiene plazo.</summary>
    public int? DiasVencido { get; set; }

    /// <summary>Escala de <see cref="EstadoCompraCxp"/>: 1 Pendiente, 2 Parcial, 3 Pagada.</summary>
    public short EstadoId { get; set; }

    /// <summary>Solo compromisos: ya pasó por «emitir pago» (<c>status_transacc</c>).</summary>
    public bool Procesado { get; set; }

    public string OrigenDescripcion => OrigenDocumentoProveedor.Describir(Origen);
    public string EstadoDescripcion => EstadoCompraCxp.Describir(EstadoId);

    public bool EsCompra => Origen == OrigenDocumentoProveedor.Compra;
    public bool EsCompromiso => Origen == OrigenDocumentoProveedor.Compromiso;

    /// <summary>Vencido de verdad: con saldo y fuera de plazo. Sin plazo nunca está vencido.</summary>
    public bool Vencido => DiasVencido is > 0 && Saldo > 0m;

    public bool TienePlazo => FechaVencimiento.HasValue;

    /// <summary>
    /// Clave del grid: el id solo no basta porque las dos ramas numeran por separado
    /// (una CxP y un compromiso pueden compartir id).
    /// </summary>
    public string ClaveGrid => $"{Origen}-{DocumentoId}";
}

/// <summary>Totales de la pantalla. Mapea 1:1 con <c>fn_prv_cxp_resumen</c>.</summary>
public sealed class CxpResumenDto
{
    public decimal SaldoTotal { get; set; }
    public decimal SaldoVencido { get; set; }

    /// <summary>Parte del saldo que vence dentro de 7 días (los documentos sin plazo no cuentan).</summary>
    public decimal SaldoVence7Dias { get; set; }

    public decimal SaldoCompras { get; set; }
    public decimal SaldoCompromisos { get; set; }

    public int DocumentosPendientes { get; set; }
    public int ComprasPendientes { get; set; }
    public int CompromisosPendientes { get; set; }
    public int DocumentosVencidos { get; set; }
}

// ── Pago en lote ────────────────────────────────────────────────────────────────

/// <summary>Un documento del lote con cuánto se le aplica.</summary>
public sealed class CxpLoteLineaDto
{
    /// <summary>Ver <see cref="OrigenDocumentoProveedor"/>.</summary>
    public short Origen { get; set; }

    public long DocumentoId { get; set; }

    /// <summary>Monto BRUTO que baja la deuda de este documento; del banco sale el neto.</summary>
    [Range(typeof(decimal), "0.01", "99999999999999.99", ErrorMessage = "El monto de cada documento debe ser mayor que cero.")]
    public decimal Monto { get; set; }

    /// <summary>Retenciones de ESTE documento (se calculan sobre su propio monto).</summary>
    public List<RetencionAplicadaDto> Retenciones { get; set; } = new();
}

/// <summary>
/// Pago de varios documentos en una sola operación. El método, la fecha y el origen del dinero
/// son únicos para todo el lote; cada documento conserva su propio abono y su comprobante.
/// </summary>
public sealed class CxpLoteUpsertDto
{
    [Required(ErrorMessage = "El método de pago es obligatorio.")]
    [StringLength(20)]
    public string MetodoPago { get; set; } = string.Empty;

    /// <summary>Cuenta bancaria de origen. Obligatoria si el método es bancario.</summary>
    public long? BancoCuentaId { get; set; }

    /// <summary>Cuenta contable de contrapartida cuando el pago no sale de un banco.</summary>
    public long? CuentaContableId { get; set; }

    public DateOnly? Fecha { get; set; }

    [StringLength(300)]
    public string? Observaciones { get; set; }

    [MinLength(1, ErrorMessage = "Seleccione al menos un documento para pagar.")]
    public List<CxpLoteLineaDto> Lineas { get; set; } = new();
}

/// <summary>Resultado de un documento dentro del lote.</summary>
public sealed class CxpLotePagoResultadoDto
{
    public short Origen { get; set; }
    public long DocumentoId { get; set; }
    public string NumeroDocumento { get; set; } = string.Empty;
    public string CodProveedor { get; set; } = string.Empty;

    public int NumeroAbono { get; set; }
    public decimal MontoAplicado { get; set; }
    public decimal Retenido { get; set; }
    public decimal Saldo { get; set; }
    public short EstadoId { get; set; }

    /// <summary>El pago dejó el documento en cero.</summary>
    public bool Saldado { get; set; }

    public decimal? NumeroCheque { get; set; }

    public string OrigenDescripcion => OrigenDocumentoProveedor.Describir(Origen);
    public string EstadoDescripcion => EstadoCompraCxp.Describir(EstadoId);
}

/// <summary>Resultado del lote completo: se registra entero o no se registra nada.</summary>
public sealed class CxpLoteResultadoDto
{
    public bool Success { get; set; }

    public decimal TotalAplicado { get; set; }
    public decimal TotalRetenido { get; set; }

    /// <summary>Lo que realmente salió del banco o de la caja: aplicado − retenido.</summary>
    public decimal TotalNeto { get; set; }

    /// <summary>Cuántos proveedores recibieron dinero: un desembolso por cada uno (D7 del plan).</summary>
    public int Desembolsos { get; set; }

    public string? Message { get; set; }

    public List<CxpLotePagoResultadoDto> Pagos { get; set; } = new();
}
