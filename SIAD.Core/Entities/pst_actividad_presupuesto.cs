namespace SIAD.Core.Entities;

public partial class pst_actividad_presupuesto
{
    public long actividad_id { get; set; }

    public long company_id { get; set; }

    public string id_presupuesto { get; set; } = null!;

    public string tipo_actividad { get; set; } = null!;

    public string estado { get; set; } = null!;

    public DateTime fecha_actividad { get; set; }

    public string? cuenta_origen_code { get; set; }

    public string cuenta_destino_code { get; set; } = null!;

    public decimal monto { get; set; }

    public string? motivo { get; set; }

    public string? referencia { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? approved_at { get; set; }

    public string? approved_by { get; set; }

    public DateTime? applied_at { get; set; }

    public string? applied_by { get; set; }
}
