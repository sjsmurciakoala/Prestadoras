using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SIAD.Core.Constants;

namespace SIAD.Core.DTOs.Almacen;

/// <summary>
/// Renglón de una orden de compra (crear/editar y mostrar).
/// <para>
/// Los <c>[Range]</c> acotan por la precisión de la columna, igual que los DTO hermanos del
/// módulo: <c>cantidad_pedida</c> y <c>costo_unitario</c> son <c>NUMERIC(14,4)</c> (10 dígitos
/// enteros) e <c>impuesto</c> es <c>NUMERIC(14,2)</c>. Sin ellos, un valor que no cabe llega a
/// Postgres y vuelve como 500 en vez de un 400 con mensaje.
/// </para>
/// </summary>
public sealed class OrdenCompraDetalleDto
{
    public int Id { get; set; }
    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string? CodigoUpc { get; set; }
    public string? CentroCosto { get; set; }
    public string? Descripcion { get; set; }

    [Range(0, 9_999_999_999d, ErrorMessage = "La cantidad pedida está fuera de rango.")]
    public decimal CantidadPedida { get; set; }

    [Range(0, 9_999_999_999d, ErrorMessage = "El costo unitario está fuera de rango.")]
    public decimal CostoUnitario { get; set; }

    [Range(0, 999_999_999_999d, ErrorMessage = "El impuesto está fuera de rango.")]
    public decimal Impuesto { get; set; }

    public decimal Total { get; set; }
    public decimal CantidadAplicada { get; set; }
}

/// <summary>Orden de compra completa (cabecera + renglones): crear, editar y ver el detalle.</summary>
public sealed class OrdenCompraDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly? Fecha { get; set; }
    public DateOnly? FechaEmision { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string? ProveedorNombre { get; set; }
    public string? TerminosPago { get; set; }
    public string? DestinoUso { get; set; }
    public bool CalculaIsv { get; set; } = true;

    /// <summary>
    /// Descuento global en PORCENTAJE. Acotado a 0–100: fuera de ese rango el total de la
    /// orden saldría negativo (o el "descuento" recargaría el pedido).
    /// </summary>
    [Range(0, 100, ErrorMessage = "El descuento debe estar entre 0 y 100 por ciento.")]
    public decimal Descuento { get; set; }

    [Range(0, 999_999_999_999d, ErrorMessage = "Los otros gastos están fuera de rango.")]
    public decimal OtrosGastos { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Impuesto { get; set; }
    public decimal Total { get; set; }
    public short Estado { get; set; } = EstadoOrdenCompra.Borrador;
    public string EstadoDescripcion => OrdenCompraEstados.Describir(Estado);
    public string? Observaciones { get; set; }
    public int? RequisicionId { get; set; }
    public string? AprobadoPor { get; set; }
    public DateTime? FechaAprobacion { get; set; }
    public List<OrdenCompraDetalleDto> Detalles { get; set; } = new();
}

/// <summary>Fila del listado de órdenes de compra.</summary>
public sealed class OrdenCompraListItemDto
{
    public int Id { get; set; }
    public int Numero { get; set; }
    public DateOnly? Fecha { get; set; }
    public string CodProveedor { get; set; } = string.Empty;
    public string? ProveedorNombre { get; set; }
    public decimal Total { get; set; }
    public short Estado { get; set; }
    public string EstadoDescripcion => OrdenCompraEstados.Describir(Estado);
    public int Renglones { get; set; }

    /// <summary>Texto para combos: "O/C 00012 — 15/07/2026 — 4,500.00".</summary>
    public string Display =>
        $"O/C {Numero:00000}" +
        (Fecha.HasValue ? $" — {Fecha.Value:dd/MM/yyyy}" : string.Empty) +
        $" — {Total:N2}";
}

/// <summary>
/// Artículo elegible para una orden de compra de un proveedor dado: sólo aparecen los que
/// existen en almacén CON su código de proveedor (regla del usuario 2026-07-30).
/// </summary>
public sealed class OrdenCompraArticuloLookupDto
{
    public int ArticuloId { get; set; }
    public string? CodigoArticulo { get; set; }
    public string Descripcion { get; set; } = string.Empty;
    /// <summary>Código con que el proveedor identifica el artículo (alm_articulo_proveedor.codigo_upc).</summary>
    public string CodigoUpc { get; set; } = string.Empty;
    /// <summary>Último costo pactado con ese proveedor; se propone como costo unitario.</summary>
    public decimal Costo { get; set; }
    public string? UnidadMedida { get; set; }

    /// <summary>"CÓDIGO — Descripción" para el combo.</summary>
    public string Display => string.IsNullOrWhiteSpace(CodigoArticulo)
        ? Descripcion
        : $"{CodigoArticulo} — {Descripcion}";
}

/// <summary>Filtro del listado de órdenes de compra.</summary>
public sealed class OrdenCompraFilterDto
{
    public string? Search { get; set; }
    public string? CodProveedor { get; set; }
    public short? Estado { get; set; }
    public DateOnly? FechaDesde { get; set; }
    public DateOnly? FechaHasta { get; set; }
}

/// <summary>Descripción legible de los estados. Los números viven en <see cref="EstadoOrdenCompra"/>.</summary>
public static class OrdenCompraEstados
{
    public static string Describir(short estado) => estado switch
    {
        EstadoOrdenCompra.Borrador        => "Borrador",
        EstadoOrdenCompra.Aprobada        => "Aprobada",
        EstadoOrdenCompra.RecibidaParcial => "Recibida parcial",
        EstadoOrdenCompra.Cerrada         => "Cerrada",
        EstadoOrdenCompra.Anulada         => "Anulada",
        _ => "—"
    };
}
