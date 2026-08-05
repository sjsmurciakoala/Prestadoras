using System;
using System.Collections.Generic;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cabecera de la recepción de compra: una factura de proveedor. Agrupa los N renglones
/// de <see cref="alm_compra"/> que comparten factura (enlazados por
/// <c>alm_compra.compra_hdr_id</c>). Migrado del flujo Centura frmFacturacionPRV /
/// PRV_FACTURAS_HDR. Multiempresa vía <see cref="ICompanyScopedEntity"/>.
/// <para>
/// Toda cabecera es un documento NUEVO (origen SIAD): el histórico migrado de SIMAFI vive
/// en <see cref="alm_compra"/> con <c>compra_hdr_id</c> NULL y no tiene cabecera.
/// </para>
/// <para>
/// La unidad de POSTEO sigue siendo la línea, no la cabecera: cada fila de
/// <see cref="alm_compra"/> lleva su propio <c>uuid</c> y genera un asiento del kardex.
/// Esta cabecera guarda lo que es del documento y no del renglón.
/// </para>
/// </summary>
public partial class alm_compra_hdr : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Correlativo interno por empresa (alm_compra_correlativo). Único por company_id.</summary>
    public int numero { get; set; }

    /// <summary>Fecha de la transacción (≡ FECHA_TRANSACCION de Centura).</summary>
    public DateOnly fecha { get; set; }

    /// <summary>Fecha que trae la factura del proveedor.</summary>
    public DateOnly? fecha_factura { get; set; }
    public DateOnly? fecha_vencimiento { get; set; }

    /// <summary>Código del proveedor (prv_proveedores es keyless/multiempresa: sin FK, se valida en el servicio).</summary>
    public string cod_proveedor { get; set; } = null!;

    /// <summary>Nombre del proveedor al momento de recibir (snapshot).</summary>
    public string? proveedor { get; set; }

    /// <summary>
    /// Número de factura del proveedor con formato SAR (admite guiones).
    /// <c>alm_compra.numero_factura</c> es NUMERIC y sólo sirve al histórico SIMAFI.
    /// </summary>
    public string? numero_factura_sar { get; set; }

    public string? cai { get; set; }

    /// <summary>O/C que se está recibiendo. NULL = compra directa (decisión D-A del usuario).</summary>
    public int? orden_compra_id { get; set; }

    /// <summary>Bodega que RECIBE la mercadería. Obligatoria: toda cabecera es un documento nuevo.</summary>
    public int bodega_id { get; set; }

    public string? terminos_pago { get; set; }

    /// <summary>Espejo de <c>alm_compra.tipo_compra</c> (contado / crédito).</summary>
    public short tipo_compra { get; set; }
    public int? plazo_dias { get; set; }

    /// <summary>HNL o USD. La 1ª entrega opera sólo en HNL.</summary>
    public string moneda { get; set; } = MonedaCompra.Lempira;

    /// <summary>Tasa de cambio; 1 = Lempiras. Siempre &gt; 0 (CHECK en BD).</summary>
    public decimal tasa_cambio { get; set; } = 1m;

    public bool consumo_interno { get; set; }

    /// <summary>
    /// Override manual del ISV por factura (≡ casilla cb3 de Centura). NULL = resolver por
    /// <c>alm_tipo_articulo.impuesto_tasa_id</c>; true = ISV separado del costo; false = capitalizado.
    /// </summary>
    public bool? detallar_isv { get; set; }

    /// <summary>Descuento global en PORCENTAJE (misma convención que la O/C).</summary>
    public decimal descuento { get; set; }
    public decimal otros_gastos { get; set; }
    public decimal flete_seguro { get; set; }
    public decimal sub_total { get; set; }
    public decimal impuesto { get; set; }
    public decimal total { get; set; }

    public string? observaciones { get; set; }

    /// <summary>Ver <see cref="EstadoRecepcionCompra"/>: 1 Registrada · 9 Anulada.</summary>
    public short estado { get; set; } = EstadoRecepcionCompra.Registrada;

    /// <summary>
    /// CACHÉ del estado: true = todos los renglones de esta factura ya generaron su asiento
    /// en alm_kardex. La garantía de no duplicar no es esta bandera, sino los índices únicos
    /// sobre uuid (aquí y en cada línea).
    /// </summary>
    public bool posteado { get; set; }

    public DateTime? fecha_posteo { get; set; }

    /// <summary>
    /// IDENTIDAD del documento, generada al CREAR la recepción (no al postear). Hace idempotente
    /// el reintento del cliente (doble clic, retry de red). El uuid del asiento de cada línea es
    /// independiente y se deriva de la línea, no de aquí.
    /// </summary>
    public Guid? uuid { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    /// <summary>Bodega de entrada (FK a alm_bodega).</summary>
    public virtual alm_bodega? bodega_ref { get; set; }

    /// <summary>Orden de compra recibida, si la recepción se hizo contra una O/C.</summary>
    public virtual alm_orden_compra? orden { get; set; }

    /// <summary>Renglones de la factura (líneas de alm_compra).</summary>
    public virtual ICollection<alm_compra> lineas { get; set; } = new List<alm_compra>();
}
