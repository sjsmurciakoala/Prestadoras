using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

public class rep_catalogo_informe : ICompanyScopedEntity
{
    public long informe_id { get; set; }

    public long company_id { get; set; }

    public string codigo { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? descripcion { get; set; }

    public string categoria { get; set; } = null!;

    public string tipo_origen { get; set; } = null!;

    public string ruta { get; set; } = null!;

    public string? consulta_clave { get; set; }

    public string? icono_css_class { get; set; }

    public int orden { get; set; }

    public bool permite_exportar { get; set; }

    public bool permite_imprimir { get; set; }

    public bool is_active { get; set; }

    public string? filtros_schema_json { get; set; }

    public string? metadata_json { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }
}
