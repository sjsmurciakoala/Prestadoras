using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Ciclo de lectura dentro de un período comercial (período × ciclo).
/// Estados numéricos: 1=ABIERTO, 2=CERRADO (EstadoPeriodoComercial).
/// El cierre valida rutas contra facturas emitidas (F7).
/// </summary>
public partial class adm_periodo_comercial_ciclo : ICompanyScopedEntity
{
    public long periodo_ciclo_id { get; set; }

    public long company_id { get; set; }

    public long periodo_comercial_id { get; set; }

    public string ciclo_codigo { get; set; } = null!;

    public short status_id { get; set; } = 1;

    public DateTime fecha_apertura { get; set; }

    public string abierto_por { get; set; } = null!;

    /// <summary>Fecha planificada de cierre (espejo de historialmes.fechacierre al abrir).</summary>
    public DateOnly? fecha_limite { get; set; }

    public DateTime? fecha_cierre { get; set; }

    public string? cerrado_por { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual adm_periodo_comercial periodo { get; set; } = null!;
}
