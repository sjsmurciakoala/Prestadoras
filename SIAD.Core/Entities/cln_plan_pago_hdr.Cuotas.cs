using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Unificación cobranza F6 (2026-07-29): el plan de pago gana tenancy real
// (company_id con filtro global + stamping) y estado numérico contra
// adm_estado_plan (1 ACTIVO, 2 COMPLETADO, 3 ANULADO — constantes en
// EstadosNumericos.EstadoPlan). estadopago (varchar) queda de solo-lectura
// legacy y se retira en F7.
public partial class cln_plan_pago_hdr : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public short estado_id { get; set; } = 1;
}
