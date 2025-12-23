using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_deprecacion : ICompanyScopedEntity
{
    public long depreciation_id { get; set; }

    public long company_id { get; set; }

    public long asset_id { get; set; }

    public long period_id { get; set; }

    public short month_number { get; set; }

    public decimal depreciation_amount { get; set; }

    public decimal accumulated_to_date { get; set; }

    public long? poliza_id { get; set; }

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public virtual con_activo_fijo? asset { get; set; }

    public virtual con_periodo_contable? period { get; set; }

    public virtual con_poliza? poliza { get; set; }
}
