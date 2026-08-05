using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cabecera del descargo (ENTREGA física de materiales). <b>ÚNICO documento del par
/// requisición/descargo que genera asientos en <c>alm_kardex</c></b> (<c>documento_tipo = DESCARGO</c>).
/// Una requisición admite N descargos (entrega parcial); un descargo sin <see cref="requisicion_hdr_id"/>
/// es una salida directa justificada por <see cref="motivo"/>. Ver
/// <c>Database/2026-08-01_alm_requisicion_descargo.sql</c>.
/// </summary>
public partial class alm_descargo_hdr : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }
    public int numero { get; set; }
    public DateOnly fecha { get; set; }

    /// <summary>Requisición que se entrega. NULL = descargo directo (sin requisición).</summary>
    public int? requisicion_hdr_id { get; set; }

    public int bodega_id { get; set; }
    public string? departamento { get; set; }
    public string? entregado_por { get; set; }
    public string? recibido_por { get; set; }

    /// <summary>Obligatorio si NO hay requisición (lo impone un CHECK).</summary>
    public string? motivo { get; set; }
    public string? observaciones { get; set; }

    /// <summary>Valorizado al costo promedio, lo estampa el posteo.</summary>
    public decimal total { get; set; }

    /// <summary>1 Registrado · 9 Anulado. Ver <see cref="SIAD.Core.Constants.EstadoDescargoHdr"/>.</summary>
    public short estado { get; set; }

    public string? motivo_anulacion { get; set; }
    public string? anulado_por { get; set; }
    public DateTime? fecha_anulacion { get; set; }

    public bool posteado { get; set; }
    public DateTime? fecha_posteo { get; set; }

    /// <summary>Idempotencia del documento.</summary>
    public Guid? uuid { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    /// <summary>Renglones de la entrega (filas de la plana con este <c>descargo_hdr_id</c>). Cada uno postea.</summary>
    public virtual ICollection<alm_descargo> lineas { get; set; } = new List<alm_descargo>();
}
