using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

// Detalle del libro fiscal de retenciones (F4, DDL 2026-08-07). Una fila por retención aplicada,
// con snapshot de código/nombre/porcentaje y la cuenta del pasivo (== cuenta de la línea POSTED).
public partial class prv_retencion_dtl : ICompanyScopedEntity
{
    public long retencion_dtl_id { get; set; }

    public long company_id { get; set; }

    public long retencion_hdr_id { get; set; }

    public int retencion_id { get; set; }

    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public decimal porcentaje { get; set; }

    public decimal base_linea { get; set; }

    public decimal monto_retenido { get; set; }

    public long account_id { get; set; }

    public virtual prv_retencion_hdr? cabecera { get; set; }
}
