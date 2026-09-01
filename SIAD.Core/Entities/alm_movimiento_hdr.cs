using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cabecera del documento de movimiento de almacén: entradas y salidas manuales
/// multi-renglón (mermas, sobrantes de conteo, consumos internos).
/// <para>
/// Equivalente del <c>dlgTransaccionesGenericasINV</c> de Centura
/// (<c>Casajaar_Final/NEWAPP/GA_IN.APT:14142</c>, menú Inventario → «Transacciones
/// genéricas»). Ver <c>docs/centura-flujos/README_entradas_salidas_almacen.md</c> §4.1b.
/// </para>
/// <para>
/// <b>Se crea y postea en un solo paso</b>: no hay estado Borrador. Un documento sin
/// asiento sería un papel sin efecto. Se corrige por <b>anulación con reversa</b>, nunca
/// por UPDATE del kardex (que es inmutable por trigger).
/// </para>
/// </summary>
public partial class alm_movimiento_hdr : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>Consecutivo por empresa, servido por <see cref="alm_movimiento_correlativo"/>.</summary>
    public int numero { get; set; }

    /// <summary>Tipo de negocio del catálogo. Su <c>clase</c> es lo que gobierna el motor.</summary>
    public int tipo_movimiento_id { get; set; }

    /// <summary>Bodega afectada. Todos los renglones del documento son de esta bodega.
    /// En un traslado es la bodega ORIGEN.</summary>
    public int bodega_id { get; set; }

    /// <summary>
    /// Bodega DESTINO, solo en clase TRASLADO (NULL en entradas/salidas normales). Debe ser
    /// distinta de <see cref="bodega_id"/> (lo impone un CHECK); la obligatoriedad en traslados la
    /// garantiza el servicio.
    /// </summary>
    public int? bodega_destino_id { get; set; }

    /// <summary>
    /// Interruptor del modo del traslado: <c>true</c> = con recepción (dos pasos, nace En tránsito);
    /// <c>false</c> = directo (un paso, nace Recibido con recepción automática). Irrelevante fuera de
    /// clase TRASLADO. DEFAULT <c>true</c> (el modo más seguro como fallback).
    /// </summary>
    public bool requiere_recepcion { get; set; }

    /// <summary>Fecha contable del asiento.</summary>
    public DateOnly fecha { get; set; }

    /// <summary>Obligatorio siempre: todo movimiento manual necesita una razón.</summary>
    public string motivo { get; set; } = null!;

    /// <summary>
    /// Referencia externa del documento de respaldo (acta de conteo, memo). Es el
    /// <c>Referencia1</c> del legacy, rotulado «#documento» en el área D. Opcional.
    /// </summary>
    public string? documento_referencia { get; set; }

    public string? observaciones { get; set; }

    /// <summary>Suma de los <c>total</c> de los renglones, calculada tras postear.</summary>
    public decimal total { get; set; }

    /// <summary>1 Registrado · 9 Anulado. Ver <see cref="SIAD.Core.Constants.EstadoMovimientoAlmacen"/>.</summary>
    public short estado { get; set; }

    public bool posteado { get; set; }
    public DateTime? fecha_posteo { get; set; }

    public string? anulado_por { get; set; }
    public DateTime? fecha_anulacion { get; set; }

    /// <summary>Traslado: usuario de la ÚLTIMA recepción, sellado cuando queda totalmente Recibido.</summary>
    public string? recibido_por { get; set; }

    /// <summary>Traslado: fecha/hora en que quedó totalmente Recibido.</summary>
    public DateTime? fecha_recepcion { get; set; }

    /// <summary>Obligatorio cuando <see cref="estado"/> es Anulado (lo impone un CHECK).</summary>
    public string? motivo_anulacion { get; set; }

    /// <summary>
    /// Idempotencia del DOCUMENTO: un reintento de la misma petición devuelve el documento
    /// ya creado en vez de duplicarlo. La del ASIENTO va por <see cref="alm_movimiento_dtl.uuid"/>.
    /// </summary>
    public Guid? uuid { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<alm_movimiento_dtl> lineas { get; set; } = new List<alm_movimiento_dtl>();

    /// <summary>Traslado: actos de recepción (uno o varios, por la recepción parcial). Vacío fuera de traslado.</summary>
    public virtual ICollection<alm_traslado_recepcion> recepciones { get; set; } = new List<alm_traslado_recepcion>();
}
