using System;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Libreta (libro del lector) — catálogo GLOBAL por empresa, sin ciclo
/// (libretas globales, 2026-07-16). En SIMAFI la libreta atraviesa los 21
/// ciclos; la combinación ciclo+libreta vive en el indicativo del cliente
/// (ciclo-barrio-libreta-secuencia), no en este catálogo.
/// Esquema en Database/2026-07-16_libretas_globales.sql.
/// </summary>
public partial class adm_libreta : ICompanyScopedEntity
{
    public long libreta_id { get; set; }

    public long company_id { get; set; }

    /// <summary>Código de la libreta (00L1..00L5). MAYÚSCULAS, sin guiones: viaja dentro del indicativo separado por '-'.</summary>
    public string codigo { get; set; } = null!;

    public string? descripcion { get; set; }

    public bool activo { get; set; } = true;

    public string created_by { get; set; } = null!;

    public DateTime created_at { get; set; }

    public string? updated_by { get; set; }

    public DateTime? updated_at { get; set; }
}
