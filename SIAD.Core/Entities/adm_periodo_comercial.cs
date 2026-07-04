using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Período comercial (empresa × año-mes). Reemplaza a <c>historialmes</c> como
/// fuente de verdad del mes comercial (plan 2026-07-02 F7, decisión D6).
/// Estados numéricos: 1=ABIERTO, 2=CERRADO (EstadoPeriodoComercial).
/// </summary>
public partial class adm_periodo_comercial : ICompanyScopedEntity
{
    public long periodo_comercial_id { get; set; }

    public long company_id { get; set; }

    public int anio { get; set; }

    public short mes { get; set; }

    public short status_id { get; set; } = 1;

    public DateTime fecha_apertura { get; set; }

    public string abierto_por { get; set; } = null!;

    public DateTime? fecha_cierre { get; set; }

    public string? cerrado_por { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<adm_periodo_comercial_ciclo> ciclos { get; set; } = new List<adm_periodo_comercial_ciclo>();
}
