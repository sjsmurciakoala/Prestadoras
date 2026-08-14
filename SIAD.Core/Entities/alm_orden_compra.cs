using System;
using System.Collections.Generic;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Orden de compra a proveedor (cabecera). El pedido se aprueba y luego se recibe
/// (la recepción vive en alm_compra, enlazada por alm_compra.orden_compra_detalle_id).
/// Migrado del flujo Centura frmOCMercaderias. Multiempresa vía ICompanyScopedEntity.
/// <para>
/// El proveedor se referencia por <see cref="cod_proveedor"/> (prv_proveedores es
/// keyless/multiempresa): sin FK en BD, su existencia se valida en el servicio.
/// </para>
/// </summary>
public partial class alm_orden_compra : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Correlativo por empresa (alm_orden_compra_correlativo). Único por company_id.</summary>
    public int numero { get; set; }

    public DateOnly? fecha { get; set; }
    public DateOnly? fecha_emision { get; set; }

    /// <summary>
    /// Fecha en que el proveedor se compromete a entregar. Obligatoria en la captura
    /// (la exige <c>OrdenCompraService</c>, no un NOT NULL): las órdenes anteriores a
    /// 2026-08-14 no la tienen y deben seguir abriéndose y recibiéndose.
    /// <para>
    /// Es lo que hace medible la puntualidad del proveedor: la recepción se compara
    /// contra esta fecha, no contra <see cref="fecha_aprobacion"/> — esa mide el ciclo
    /// interno, no el cumplimiento del proveedor.
    /// </para>
    /// </summary>
    public DateOnly? fecha_entrega_pactada { get; set; }

    public string cod_proveedor { get; set; } = null!;
    public string? terminos_pago { get; set; }

    /// <summary>Destino / "Para uso de" (≡ PUNTO_DE_VENTA de Centura).</summary>
    public string? destino_uso { get; set; }

    public bool calcula_isv { get; set; } = true;

    /// <summary>Descuento global en PORCENTAJE (decisión usuario 2026-07-30).</summary>
    public decimal descuento { get; set; }
    public decimal otros_gastos { get; set; }
    public decimal sub_total { get; set; }
    public decimal impuesto { get; set; }
    public decimal total { get; set; }

    /// <summary>Ver <see cref="EstadoOrdenCompra"/>: 1 Borrador · 2 Aprobada · 3 Recibida parcial · 4 Cerrada · 9 Anulada.</summary>
    public short estado { get; set; } = EstadoOrdenCompra.Borrador;

    public string? observaciones { get; set; }

    /// <summary>Requisición de origen si la O/C nació de una (alm_requisicion). NULL si se capturó directa.</summary>
    public int? requisicion_id { get; set; }

    /// <summary>Usuario que aprobó (aprobación por permiso de rol). NULL mientras está en borrador.</summary>
    public string? aprobado_por { get; set; }
    public DateTime? fecha_aprobacion { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    /// <summary>Renglones del pedido.</summary>
    public virtual ICollection<alm_orden_compra_detalle> detalles { get; set; } = new List<alm_orden_compra_detalle>();
}
