using System;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Compra de artículo de almacén (línea por artículo, con datos de
/// proveedor/factura repetidos por fila). Migrado de MySQL bdsimafi.compras.
/// <para>
/// Documento origen de asientos del kardex: al postearse genera un INGRESO a
/// <see cref="bodega_id"/>. Las filas con <see cref="origen"/> = SIMAFI son el
/// histórico migrado, blindado con <see cref="posteado"/> = true — el motor nunca
/// las postea (duplicaría el inventario).
/// </para>
/// </summary>
public partial class alm_compra : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public DateOnly? fecha { get; set; }
    public DateOnly? fecha_factura { get; set; }
    public string? codigo_articulo { get; set; }
    public int? articulo_id { get; set; }
    public decimal cantidad { get; set; }
    public decimal precio_unitario { get; set; }
    public decimal precio_unitario_anterior { get; set; }
    public decimal total { get; set; }
    public decimal impuesto { get; set; }
    public decimal descuento { get; set; }
    public string? oficina { get; set; }
    public string? proveedor { get; set; }
    public decimal? numero_factura { get; set; }
    public decimal? numero { get; set; }
    public string? orden_compra { get; set; }
    public decimal? plazo_dias { get; set; }
    public short tipo_compra { get; set; }
    public string? traslado { get; set; }
    public string? cuenta_contable { get; set; }
    public string? cuenta_contable_anterior { get; set; }
    public string? cuenta_por_pagar { get; set; }
    public string? cuenta_por_pagar_anterior { get; set; }
    public string? codigo_compra { get; set; }
    public string? concepto { get; set; }

    /// <summary>
    /// Bodega que RECIBE la mercadería (FK a alm_bodega). Obligatoria en documentos
    /// SIAD (CHECK <c>ck_alm_compra_bodega_si_siad</c>); NULL en el histórico SIMAFI,
    /// que no la traía.
    /// </summary>
    public int? bodega_id { get; set; }

    /// <summary>
    /// CACHÉ del estado: true = esta línea ya generó su asiento en alm_kardex. La garantía
    /// de no duplicar NO es esta bandera, sino el índice único sobre <see cref="uuid"/>.
    /// El histórico SIMAFI viene con true (blindaje: su existencia ya está en el inventario,
    /// postearlo lo duplicaría).
    /// </summary>
    public bool posteado { get; set; }

    /// <summary>
    /// Fecha/hora UTC en que el motor posteó esta línea. NULL mientras no se postee,
    /// y siempre en el histórico SIMAFI (nunca pasó por el motor).
    /// </summary>
    public DateTime? fecha_posteo { get; set; }

    /// <summary>
    /// IDENTIDAD del documento-línea (tabla plana: una fila = una línea), generada al
    /// <b>CREAR</b> el documento, NO al postearlo. Inmutable. Es lo que hace idempotente
    /// el reintento del cliente (doble clic, retry de red): el mismo uuid re-enviado no
    /// crea una segunda línea (índice único por company_id + uuid).
    /// <para>
    /// El uuid del asiento en alm_kardex se DERIVA de éste:
    /// <c>UUIDv5(documento_tipo | uuid | evento)</c>, donde <c>evento</c> distingue emisión
    /// de recepción — así una compra que genera dos asientos (tránsito y existencia) no
    /// colisiona consigo misma.
    /// </para>
    /// NULL solo en el histórico SIMAFI.
    /// </summary>
    public Guid? uuid { get; set; }

    /// <summary>
    /// Origen del documento. Vocabulario cerrado: ver <see cref="OrigenDocumento"/>.
    /// SIMAFI = histórico migrado (nunca posteable) | SIAD = creado por el sistema nuevo.
    /// Inmutable una vez fijado (trigger <c>trg_alm_compra_blindaje</c>, SQLSTATE K0002).
    /// <para>
    /// La columna NO tiene DEFAULT en la BD, a propósito: toda importación SIMAFI debe
    /// declarar el origen explícitamente. Este inicializador es lo que hace que EF siempre
    /// mande <c>origen</c> en el INSERT.
    /// </para>
    /// </summary>
    public string origen { get; set; } = OrigenDocumento.Siad;

    /// <summary>Artículo de la compra (FK a alm_articulo). El código legacy se conserva como referencia.</summary>
    public virtual alm_articulo? articulo_ref { get; set; }

    /// <summary>Bodega de entrada de la compra (FK a alm_bodega).</summary>
    public virtual alm_bodega? bodega_ref { get; set; }
}
