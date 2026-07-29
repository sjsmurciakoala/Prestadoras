using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class pst_config_presupuesto_dtl : ICompanyScopedEntity
{
    public long company_id { get; set; }

    public string id_presupuesto { get; set; } = null!;

    public long id_presupuesto_dtl { get; set; }

    public string con_cuenta_code { get; set; } = null!;

    public decimal valor_proyeccion { get; set; }

    public decimal valor_real { get; set; }

    public decimal valor_disponible { get; set; }
}
