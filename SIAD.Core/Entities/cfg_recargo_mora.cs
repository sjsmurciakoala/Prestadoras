using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cfg_recargo_mora
{
    public long company_id { get; set; }

    public decimal tasa_mensual { get; set; }

    public int dias_gracia { get; set; }

    public string? descripcion { get; set; }

    public bool activo { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
