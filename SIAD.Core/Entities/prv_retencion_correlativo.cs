using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Contador de folio de retención por empresa (F4, DDL 2026-08-07). PK = company_id.
// El folio se reserva con upsert atómico en la transacción del pago (no vía EF SaveChanges).
public partial class prv_retencion_correlativo : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public int ultimo_folio { get; set; }
}
