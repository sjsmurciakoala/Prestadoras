using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Caja física (ventanilla) por empresa. Cada sesión de caja se abre EN una
// caja; varias cajas operan simultáneamente, cada una con su cajero y arqueo.
// Unificación cobranza F2 (2026-07-26). Reemplaza a catalogo_cajas (legacy).
public partial class adm_caja : ICompanyScopedEntity
{
    public int caja_id { get; set; }

    public long company_id { get; set; }

    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public bool activo { get; set; }

    public DateTime creado_en { get; set; }

    public DateTime? actualizado_en { get; set; }

    public string? updated_by { get; set; }
}
