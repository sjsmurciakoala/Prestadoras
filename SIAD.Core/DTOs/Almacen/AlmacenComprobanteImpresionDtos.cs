namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Base de impresión de los comprobantes de almacén (requisición y descargo): el encabezado de la
/// empresa emisora + quién imprime. Los datos de empresa salen de <c>cfg_company</c> (igual que el
/// comprobante de compromiso). El comprobante concreto agrega su documento y el total en letras.
/// </summary>
public abstract class ComprobanteAlmacenImpresionBase
{
    public string EmpresaNombre { get; set; } = string.Empty;
    public string? EmpresaRazonSocial { get; set; }
    public string? EmpresaRtn { get; set; }
    public string? EmpresaDireccion { get; set; }
    public string? EmpresaTelefono { get; set; }
    public string? EmpresaEmail { get; set; }
    public byte[]? EmpresaLogo { get; set; }

    /// <summary>Usuario que solicitó la impresión (aparece en el pie del comprobante).</summary>
    public string ImpresoPor { get; set; } = string.Empty;
}

/// <summary>
/// Datos de impresión del comprobante (vale) de una requisición de materiales. La requisición es la
/// solicitud: se imprime para firmarla y darle seguimiento al despacho, no mueve inventario.
/// </summary>
public sealed class RequisicionImpresionDto : ComprobanteAlmacenImpresionBase
{
    public RequisicionDocumentoDto Documento { get; set; } = new();

    /// <summary>Total de la requisición en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string MontoEnLetras { get; set; } = string.Empty;
}

/// <summary>
/// Datos de impresión del comprobante (vale de salida) de un descargo. El descargo es la entrega real:
/// el total y su expresión en letras corresponden al valor de la mercadería que salió del kardex.
/// </summary>
public sealed class DescargoImpresionDto : ComprobanteAlmacenImpresionBase
{
    public DescargoDocumentoDto Documento { get; set; } = new();

    /// <summary>Total del descargo en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string MontoEnLetras { get; set; } = string.Empty;
}

/// <summary>
/// Datos de impresión del comprobante de un movimiento de almacén genérico (entrada / salida /
/// ajuste de valor). El movimiento SÍ postea al kardex; el vale lo respalda con su concepto, los
/// renglones y —cuando hay valor— el total y su expresión en letras.
/// </summary>
public sealed class MovimientoImpresionDto : ComprobanteAlmacenImpresionBase
{
    public MovimientoAlmacenDto Documento { get; set; } = new();

    /// <summary>Total del movimiento en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string MontoEnLetras { get; set; } = string.Empty;
}

/// <summary>
/// Datos de impresión de la PARTIDA CONTABLE (asiento de doble entrada) que generó un movimiento
/// de almacén. Se arma desde <c>con_partida_hdr</c>/<c>con_partida_dtl</c> vía la póliza del
/// documento (module ALMACEN / docType MOVIMIENTO / id del movimiento). Existe solo si el módulo
/// de almacén está integrado a contabilidad y hubo período abierto al postear.
/// </summary>
public sealed class PartidaContableImpresionDto : ComprobanteAlmacenImpresionBase
{
    /// <summary>Número de póliza asignado por el motor contable (<c>poliza_number</c>).</summary>
    public string Numero { get; set; } = string.Empty;

    public DateTime Fecha { get; set; }

    /// <summary>Concepto de la partida (descripción del asiento).</summary>
    public string? Descripcion { get; set; }

    /// <summary>Documento origen legible, p. ej. "Movimiento No. 00042".</summary>
    public string DocumentoReferencia { get; set; } = string.Empty;

    /// <summary>REGISTRADA / ANULADA — para la caja del encabezado.</summary>
    public string EstadoTexto { get; set; } = string.Empty;

    /// <summary>La partida está revertida (documento anulado): el reporte pinta la marca de agua.</summary>
    public bool Anulada { get; set; }

    public decimal TotalDebe { get; set; }
    public decimal TotalHaber { get; set; }

    public List<PartidaContableLineaImpresionDto> Lineas { get; set; } = new();

    /// <summary>Total de la partida en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string TotalEnLetras { get; set; } = string.Empty;
}

/// <summary>
/// Datos de impresión del comprobante de un PAGO (abono) a una cuenta por pagar de compra: el
/// documento operativo del egreso (proveedor, factura, método, banco/cheque, saldos). El asiento
/// contable del pago se imprime aparte, con <see cref="PartidaContableImpresionDto"/>.
/// </summary>
public sealed class PagoCompraImpresionDto : ComprobanteAlmacenImpresionBase
{
    public int NumeroAbono { get; set; }
    public DateOnly Fecha { get; set; }

    public string CodProveedor { get; set; } = string.Empty;
    public string? Proveedor { get; set; }

    /// <summary>No. de factura del proveedor (SAR) o correlativo interno de la compra.</summary>
    public string NumeroFactura { get; set; } = string.Empty;
    public decimal MontoFactura { get; set; }

    public decimal Monto { get; set; }
    public decimal SaldoAnterior { get; set; }
    public decimal SaldoRestante { get; set; }

    /// <summary>Método de pago legible (Efectivo / Cheque / Transferencia).</summary>
    public string MetodoPago { get; set; } = string.Empty;

    /// <summary>Banco y cuenta de origen cuando el pago es bancario; nulos si es en efectivo.</summary>
    public string? Banco { get; set; }
    public string? CuentaBancaria { get; set; }
    public string? NumCheque { get; set; }

    /// <summary>No. de la partida contable del pago, si generó asiento (nulo si no).</summary>
    public string? NumeroPartida { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>El pago está anulado: el reporte pinta la marca de agua.</summary>
    public bool Anulada { get; set; }

    /// <summary>VIGENTE / ANULADO — para la caja del encabezado.</summary>
    public string EstadoTexto { get; set; } = string.Empty;

    /// <summary>Monto del pago en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string MontoEnLetras { get; set; } = string.Empty;
}

/// <summary>Renglón del asiento: cuenta (código + nombre) y su importe al Debe o al Haber.</summary>
public sealed class PartidaContableLineaImpresionDto
{
    public string CuentaCodigo { get; set; } = string.Empty;
    public string CuentaNombre { get; set; } = string.Empty;
    public decimal Debe { get; set; }
    public decimal Haber { get; set; }
    public string? Descripcion { get; set; }
}

/// <summary>
/// Datos de impresión del comprobante de ENVÍO de un traslado entre bodegas (directo o en tránsito):
/// la mercadería que sale de la bodega origen hacia la destino. En el traslado con recepción este vale
/// acompaña la salida; cada recepción en destino se respalda con <see cref="TrasladoRecepcionImpresionDto"/>.
/// </summary>
public sealed class TrasladoImpresionDto : ComprobanteAlmacenImpresionBase
{
    public TrasladoDto Documento { get; set; } = new();

    /// <summary>Total del traslado en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string MontoEnLetras { get; set; } = string.Empty;
}

/// <summary>
/// Datos de impresión del comprobante de una RECEPCIÓN de traslado (la tanda que recibe la bodega
/// destino, posiblemente parcial). Lleva el traslado como contexto y el acto de recepción concreto.
/// </summary>
public sealed class TrasladoRecepcionImpresionDto : ComprobanteAlmacenImpresionBase
{
    public TrasladoDto Traslado { get; set; } = new();
    public TrasladoRecepcionDto Recepcion { get; set; } = new();

    /// <summary>Total recibido en esta tanda, en letras (sin sufijo "LEMPIRAS", lo agrega el reporte).</summary>
    public string MontoEnLetras { get; set; } = string.Empty;
}
