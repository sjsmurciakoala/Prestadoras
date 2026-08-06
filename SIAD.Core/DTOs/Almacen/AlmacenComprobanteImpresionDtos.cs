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
