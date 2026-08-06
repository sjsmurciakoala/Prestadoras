using System;
using System.Collections.Generic;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Recepción de compra: la factura del proveedor con sus renglones. Es el documento que
/// entra la mercadería al inventario (una línea de <c>alm_compra</c> por renglón, cada una
/// con su asiento en el kardex).
/// <para>
/// Dos modos: <b>con orden de compra</b> (<see cref="OrdenCompraId"/> con valor) y
/// <b>compra directa</b> (sin O/C). Una misma O/C admite VARIAS facturas: cada recepción
/// descarga lo que recibe y la orden se cierra cuando todos sus renglones quedan cubiertos.
/// </para>
/// </summary>
public sealed class RecepcionCompraDto
{
    public int Id { get; set; }

    /// <summary>Correlativo interno por empresa. Lo asigna el servidor.</summary>
    public int Numero { get; set; }

    /// <summary>Fecha de la transacción. Si viene nula, el servidor usa hoy.</summary>
    public DateOnly? Fecha { get; set; }

    public DateOnly? FechaFactura { get; set; }
    public DateOnly? FechaVencimiento { get; set; }

    public string CodProveedor { get; set; } = string.Empty;

    /// <summary>Nombre del proveedor. Solo lectura: lo resuelve el servidor.</summary>
    public string? ProveedorNombre { get; set; }

    /// <summary>Número de factura del proveedor (formato SAR, admite guiones).</summary>
    public string? NumeroFacturaSar { get; set; }

    public string? Cai { get; set; }

    /// <summary>O/C que se recibe. NULL = compra directa.</summary>
    public int? OrdenCompraId { get; set; }

    /// <summary>Número de la O/C recibida. Solo lectura.</summary>
    public int? OrdenCompraNumero { get; set; }

    /// <summary>Bodega que recibe la mercadería. Obligatoria.</summary>
    public int BodegaId { get; set; }

    public string? BodegaNombre { get; set; }

    public string? TerminosPago { get; set; }
    public short TipoCompra { get; set; }
    public int? PlazoDias { get; set; }

    public string Moneda { get; set; } = MonedaCompra.Lempira;
    public decimal TasaCambio { get; set; } = 1m;

    public bool ConsumoInterno { get; set; }

    /// <summary>
    /// Override del ISV por factura. NULL = usar la política de la empresa
    /// (<c>cfg_compra_isv</c>); true = ISV separado del costo; false = capitalizado al costo.
    /// </summary>
    public bool? DetallarIsv { get; set; }

    /// <summary>Descuento global en PORCENTAJE (0–100).</summary>
    public decimal Descuento { get; set; }
    public decimal OtrosGastos { get; set; }
    public decimal FleteSeguro { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Total { get; set; }

    public string? Observaciones { get; set; }

    /// <summary>Ver <see cref="EstadoRecepcionCompra"/>: 1 Registrada · 9 Anulada.</summary>
    public short Estado { get; set; } = EstadoRecepcionCompra.Registrada;

    public string EstadoDescripcion => RecepcionCompraEstados.Describir(Estado);

    public bool Posteado { get; set; }

    /// <summary>
    /// Identidad del documento para idempotencia del alta. Si el cliente la manda y ya
    /// existe una recepción con ese uuid en la empresa, el servidor devuelve ESA en vez de
    /// crear otra (protege contra doble clic y reintentos de red).
    /// </summary>
    public Guid? Uuid { get; set; }

    public List<RecepcionCompraDetalleDto> Detalles { get; set; } = new();
}

/// <summary>Renglón de la factura: un artículo recibido.</summary>
public sealed class RecepcionCompraDetalleDto
{
    /// <summary>Id de la línea (<c>alm_compra.id</c>). Solo lectura.</summary>
    public int Id { get; set; }

    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? Descripcion { get; set; }

    /// <summary>Código del artículo para este proveedor. Solo lectura: sale del catálogo.</summary>
    public string? CodigoUpc { get; set; }

    /// <summary>Renglón de la O/C que se descarga con esta línea. NULL en compra directa.</summary>
    public int? OrdenDetalleId { get; set; }

    /// <summary>Cantidad recibida (facturada).</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Costo unitario SIN ISV, tal como lo factura el proveedor.</summary>
    public decimal CostoUnitario { get; set; }

    /// <summary>ISV del renglón. Lo calcula el servidor por el tipo de artículo.</summary>
    public decimal Impuesto { get; set; }

    public decimal Descuento { get; set; }

    /// <summary>Total del renglón (cantidad × costo). Lo calcula el servidor.</summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Costo con el que la línea entró al kardex: incluye el ISV si la política lo
    /// capitaliza. Solo lectura.
    /// </summary>
    public decimal CostoPosteado { get; set; }
}

/// <summary>Fila del listado de recepciones.</summary>
public sealed class RecepcionCompraListItemDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly Fecha { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string? ProveedorNombre { get; set; }
    public string? NumeroFacturaSar { get; set; }
    public int? OrdenCompraNumero { get; set; }
    public string? BodegaNombre { get; set; }
    public decimal Total { get; set; }
    public short Estado { get; set; }
    public string EstadoDescripcion => RecepcionCompraEstados.Describir(Estado);
    public bool Posteado { get; set; }
    public int Renglones { get; set; }
}

/// <summary>Etiquetas de los estados de una recepción, para la UI.</summary>
public static class RecepcionCompraEstados
{
    public static string Describir(short estado) => estado switch
    {
        EstadoRecepcionCompra.Registrada => "Registrada",
        EstadoRecepcionCompra.Anulada    => "Anulada",
        _ => "—"
    };
}

/// <summary>Filtros del listado.</summary>
public sealed class RecepcionCompraFilterDto
{
    public string? CodProveedor { get; set; }
    public short? Estado { get; set; }
    public int? OrdenCompraId { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
    public string? Search { get; set; }
}

/// <summary>
/// Renglón de una O/C con lo que todavía falta recibir. Es lo que llena la grilla en el modo
/// Con orden de compra; como la orden admite varias facturas, lo pendiente se recalcula en
/// cada recepción.
/// </summary>
public sealed class OrdenCompraPendienteDto
{
    public int OrdenDetalleId { get; set; }
    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? Descripcion { get; set; }
    public string? CodigoUpc { get; set; }
    public decimal CantidadPedida { get; set; }
    public decimal CantidadAplicada { get; set; }
    public decimal CantidadPendiente { get; set; }
    public decimal CostoUnitario { get; set; }
}
