using System;
using System.Collections.Generic;

using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public partial class servicio : ICompanyScopedEntity
{
    public int servicios_id { get; set; }

    public long company_id { get; set; }

    public string servicios_codigo { get; set; } = null!;

    public string servicios_descripcioncorta { get; set; } = null!;

    public string? servicios_descripcionlarga { get; set; }

    public bool estado { get; set; }

    public bool es_servicio_base { get; set; }

    public bool facturable_app { get; set; }

    public int app_orden { get; set; }

    public string? app_grupo { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public long? cont_account_id { get; set; }

    public virtual con_plan_cuenta? cont_account { get; set; }
}
