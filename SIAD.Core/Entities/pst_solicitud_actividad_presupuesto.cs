namespace SIAD.Core.Entities;

public partial class pst_solicitud_actividad_presupuesto
{
    public long solicitud_id { get; set; }

    public long company_id { get; set; }

    public string id_presupuesto { get; set; } = null!;

    public string tipo_actividad { get; set; } = null!;

    public string? cuenta_origen_code { get; set; }

    public string cuenta_destino_code { get; set; } = null!;

    public decimal monto { get; set; }

    public string justificacion { get; set; } = null!;

    public short prioridad { get; set; }

    public string estado { get; set; } = null!;

    public DateOnly? fecha_necesaria { get; set; }

    public string solicitado_por { get; set; } = null!;

    public DateTime solicitado_en { get; set; }

    public string? revisado_por { get; set; }

    public DateTime? revisado_en { get; set; }

    public string? comentario_revision { get; set; }

    public long? actividad_id { get; set; }
}
