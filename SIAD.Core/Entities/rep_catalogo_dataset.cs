using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class rep_catalogo_dataset : ICompanyScopedEntity
{
    public long dataset_id { get; set; }

    public long company_id { get; set; }

    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? descripcion { get; set; }

    public string tipo_origen { get; set; } = null!;

    public string? origen_clave { get; set; }

    public string? sql_text { get; set; }

    public string? connection_name { get; set; }

    public bool is_active { get; set; }

    public string? metadata_json { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
