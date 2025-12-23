using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class con_tercero : ICompanyScopedEntity
{
    public long third_party_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = string.Empty;

    public string name { get; set; } = string.Empty;

    public string? description { get; set; }

    public string? tax_id { get; set; }

    public string category { get; set; } = string.Empty;

    public bool is_supplier { get; set; }

    public bool is_customer { get; set; }

    public string status { get; set; } = "ACTIVE";

    public DateTime created_at { get; set; } = DateTime.UtcNow;

    public string created_by { get; set; } = string.Empty;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<con_libro_iva> iva_registros { get; set; } = new List<con_libro_iva>();
}
