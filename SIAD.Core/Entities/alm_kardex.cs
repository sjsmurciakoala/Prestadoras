using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Kardex de movimientos de bodega (ingresos/salidas por artículo).
/// Migrado de MySQL bdsimafi.inventariotra; se relaciona con alm_articulo
/// por codigo_articulo (no con af_activo_fijo pese al nombre de origen).
/// <para>
/// LIBRO MAYOR INMUTABLE: solo INSERT. La BD rechaza UPDATE y DELETE
/// (trigger <c>trg_alm_kardex_inmutable</c>, SQLSTATE K0001); las correcciones se
/// hacen posteando un contra-asiento (reversa). Las filas con <see cref="uuid"/>
/// NULL son el histórico SIMAFI, no posteado por el motor.
/// </para>
/// </summary>
public partial class alm_kardex : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public decimal? numero_documento { get; set; }
    public string? tipo_transaccion { get; set; }
    public DateOnly? fecha { get; set; }
    public string? codigo_articulo { get; set; }
    public int? articulo_id { get; set; }
    public decimal cantidad { get; set; }
    public string? bodega { get; set; }
    public int? bodega_id { get; set; }
    public decimal ingresos { get; set; }
    public decimal salidas { get; set; }
    public decimal valor_unitario { get; set; }
    public decimal total { get; set; }
    public decimal debe { get; set; }
    public decimal haber { get; set; }
    public string? cuenta_contable { get; set; }
    public string? departamento { get; set; }
    public string? departamento_desc { get; set; }
    public string? linea { get; set; }
    public string? linea_desc { get; set; }
    public string? barrio { get; set; }
    public bool es_ajuste { get; set; }
    public string? descripcion { get; set; }
    public string? observacion { get; set; }

    /// <summary>
    /// Idempotencia por MOVIMIENTO (línea), no por documento. Determinista desde la
    /// identidad del movimiento: (documento_tipo, documento_id, línea), o
    /// (articulo_id, bodega_id) en CARGA_INICIAL. Reintentar el mismo posteo no duplica
    /// el asiento (índice único por company_id + uuid). NULL = histórico SIMAFI,
    /// no posteado por el motor.
    /// </summary>
    public Guid? uuid { get; set; }

    /// <summary>
    /// Tipo del documento que originó el asiento. Vocabulario cerrado:
    /// ver <see cref="SIAD.Core.Constants.TipoDocumentoInventario"/>.
    /// </summary>
    public string? documento_tipo { get; set; }

    /// <summary>Id del documento origen dentro de la tabla que corresponde a <see cref="documento_tipo"/>.</summary>
    public int? documento_id { get; set; }

    /// <summary>
    /// Solo referencia informativa en traslados. La bodega AFECTADA por este asiento es
    /// SIEMPRE <see cref="bodega_id"/>; un traslado se postea como DOS asientos
    /// (envío y recepción), uno por bodega.
    /// </summary>
    public int? bodega_destino_id { get; set; }

    /// <summary>Saldo de existencia del par (articulo_id, bodega_id) DESPUÉS de este asiento.</summary>
    public decimal? existencia_resultante { get; set; }

    /// <summary>Costo promedio del par (articulo_id, bodega_id) DESPUÉS de este asiento.</summary>
    public decimal? costo_promedio_resultante { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }

    /// <summary>
    /// Bodega normalizada (FK a alm_bodega). La columna legacy <see cref="bodega"/>
    /// (VARCHAR de texto libre migrado) se conserva; ésta es la referencia real al catálogo.
    /// </summary>
    public virtual alm_bodega? bodega_ref { get; set; }

    /// <summary>
    /// Bodega destino informativa del movimiento (FK a alm_bodega), usada en traslados.
    /// No es la bodega afectada por este asiento — esa es <see cref="bodega_ref"/>.
    /// </summary>
    public virtual alm_bodega? bodega_destino_ref { get; set; }

    /// <summary>
    /// Artículo del movimiento (FK a alm_articulo). Referencia real; la columna legacy
    /// <see cref="codigo_articulo"/> se conserva como snapshot/referencia SIMAFI.
    /// </summary>
    public virtual alm_articulo? articulo_ref { get; set; }
}
