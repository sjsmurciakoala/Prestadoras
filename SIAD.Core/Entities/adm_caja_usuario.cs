using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Asignación cajero → caja física. Un usuario pertenece a UNA caja; una caja
// admite varios usuarios (turnos). Unificación cobranza F3 (2026-07-26).
public partial class adm_caja_usuario : ICompanyScopedEntity
{
    public int caja_usuario_id { get; set; }

    public long company_id { get; set; }

    public int caja_id { get; set; }

    public string usuario { get; set; } = null!;

    public DateTime creado_en { get; set; }

    public string? updated_by { get; set; }
}
