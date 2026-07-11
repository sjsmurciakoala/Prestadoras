using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class prv_compromiso_dtl : ICompanyScopedEntity
{
    public long compromiso_dtl_id { get; set; }

    public long company_id { get; set; }

    public int numero_orden { get; set; }

    public string cod_presupuestario { get; set; } = null!;

    public string programa { get; set; } = null!;

    public string actividad { get; set; } = null!;

    public string objeto_gasto { get; set; } = null!;

    public string cuenta_gasto { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public decimal monto { get; set; }

    public string? conceptodtl { get; set; }
}
