using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Cabecera de un acto de recepción de un traslado entre bodegas. Un traslado con recepción se
/// recibe en una o varias tandas (recepción parcial); cada tanda es una fila con su fecha, usuario
/// y <see cref="uuid"/> de idempotencia. Un traslado directo genera una recepción automática.
/// <para>Ver <c>docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md</c> §2.3.</para>
/// </summary>
public partial class alm_traslado_recepcion : ICompanyScopedEntity
{
    public int id { get; set; }
    public long company_id { get; set; }

    /// <summary>El traslado que se recibe (<see cref="alm_movimiento_hdr"/>).</summary>
    public int movimiento_hdr_id { get; set; }

    public DateOnly fecha { get; set; }
    public string? observaciones { get; set; }

    /// <summary>Idempotencia del ACTO de recepción: un reintento con el mismo uuid no vuelve a postear.</summary>
    public Guid uuid { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }

    public virtual alm_movimiento_hdr cabecera { get; set; } = null!;
    public virtual ICollection<alm_traslado_recepcion_dtl> lineas { get; set; } = new List<alm_traslado_recepcion_dtl>();
}
