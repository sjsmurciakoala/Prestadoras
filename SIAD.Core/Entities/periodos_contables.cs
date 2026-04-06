namespace SIAD.Core.Entities;

public partial class periodos_contables
{
    public int id { get; set; }

    public string? periodo { get; set; }

    public int? anio { get; set; }

    public int? mes { get; set; }

    public bool activo { get; set; }

    public DateTime? fecha_inicio { get; set; }

    public DateTime? fecha_fin { get; set; }
}
