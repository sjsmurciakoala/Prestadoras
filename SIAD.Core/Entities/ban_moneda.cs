using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_moneda
{
    public long ban_moneda_id { get; set; }

    public long company_id { get; set; }

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public string? pais { get; set; }

    public decimal factor { get; set; }

    public bool es_base { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual cfg_company company { get; set; } = null!;
}
