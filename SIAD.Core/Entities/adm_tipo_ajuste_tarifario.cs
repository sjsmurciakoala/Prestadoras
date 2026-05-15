using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class adm_tipo_ajuste_tarifario
{
    public long tipo_ajuste_tarifario_id { get; set; }

    public long company_id { get; set; }

    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? descripcion { get; set; }

    public short status_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<adm_ajuste_tarifario> adm_ajuste_tarifarios { get; set; } = new List<adm_ajuste_tarifario>();
}
