using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class pst_config_presupuesto_hdr : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public string id_presupuesto { get; set; } = null!;

    public decimal valor_global { get; set; }

    public decimal valor_disponible { get; set; }

    public int rango_periodo { get; set; }

    public DateOnly fecha_inicia { get; set; }

    public DateOnly fecha_finaliza { get; set; }

    public bool estado_aprobado { get; set; }
}
