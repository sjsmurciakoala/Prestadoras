using System;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Descargo (salida/consumo) de artículo de almacén hacia un departamento.
/// Migrado de MySQL bdsimafi.descargos.
/// <para>
/// Documento origen de asientos del kardex: al postearse genera una SALIDA de
/// <see cref="bodega_id"/>. Las filas con <see cref="origen"/> = SIMAFI son el
/// histórico migrado, blindado con <see cref="posteado"/> = true — el motor nunca
/// las postea (duplicaría el inventario).
/// </para>
/// </summary>
public partial class alm_descargo : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public DateOnly? fecha { get; set; }
    public string? codigo_articulo { get; set; }
    public int? articulo_id { get; set; }
    public decimal cantidad { get; set; }
    public decimal precio_unitario { get; set; }
    public decimal total { get; set; }
    public string? oficina { get; set; }
    public string? departamento { get; set; }
    public decimal? numero_requisicion { get; set; }
    public decimal? numero_documento { get; set; }
    public short? tipo_requisicion { get; set; }
    public string? traslado { get; set; }
    public string? cuenta_contable_1 { get; set; }
    public string? cuenta_contable_1_detalle { get; set; }
    public string? cuenta_contable_2 { get; set; }
    public string? cuenta_contable_2_detalle { get; set; }
    public string? comentario { get; set; }

    /// <summary>
    /// Bodega de la que SALE la mercadería (FK a alm_bodega). Obligatoria en documentos
    /// SIAD (CHECK <c>ck_alm_descargo_bodega_si_siad</c>); NULL en el histórico SIMAFI,
    /// que no la traía.
    /// </summary>
    public int? bodega_id { get; set; }

    /// <summary>
    /// CACHÉ del estado: true = esta línea ya generó su asiento en alm_kardex. La garantía
    /// de no duplicar NO es esta bandera, sino el índice único sobre <see cref="uuid"/>.
    /// El histórico SIMAFI viene con true (blindaje).
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
    /// el reintento del cliente (índice único por company_id + uuid).
    /// <para>
    /// El uuid del asiento en alm_kardex se DERIVA de éste:
    /// <c>UUIDv5(documento_tipo | uuid | evento)</c>.
    /// </para>
    /// NULL solo en el histórico SIMAFI.
    /// </summary>
    public Guid? uuid { get; set; }

    /// <summary>
    /// Origen del documento. Vocabulario cerrado: ver <see cref="OrigenDocumento"/>.
    /// SIMAFI = histórico migrado (nunca posteable) | SIAD = creado por el sistema nuevo.
    /// Inmutable una vez fijado (trigger <c>trg_alm_descargo_blindaje</c>, SQLSTATE K0002).
    /// <para>
    /// La columna NO tiene DEFAULT en la BD, a propósito: toda importación SIMAFI debe
    /// declarar el origen explícitamente. Este inicializador es lo que hace que EF siempre
    /// mande <c>origen</c> en el INSERT.
    /// </para>
    /// </summary>
    public string origen { get; set; } = OrigenDocumento.Siad;

    /// <summary>Artículo del descargo (FK a alm_articulo). El código legacy se conserva como referencia.</summary>
    public virtual alm_articulo? articulo_ref { get; set; }

    /// <summary>Bodega de salida del descargo (FK a alm_bodega).</summary>
    public virtual alm_bodega? bodega_ref { get; set; }
}
