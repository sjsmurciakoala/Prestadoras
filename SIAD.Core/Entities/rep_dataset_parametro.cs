using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class rep_dataset_parametro : ICompanyScopedEntity
{
    public long dataset_parametro_id { get; set; }

    public long company_id { get; set; }

    public long dataset_id { get; set; }

    public string nombre { get; set; } = null!;

    public string etiqueta { get; set; } = null!;

    public string tipo_dato { get; set; } = null!;

    public string? nombre_origen { get; set; }

    public string fuente_valor { get; set; } = null!;

    public string? valor_default { get; set; }

    public bool visible { get; set; }

    public bool permite_nulo { get; set; }

    public bool requerido { get; set; }

    public int orden { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
