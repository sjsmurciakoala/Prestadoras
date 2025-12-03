using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class maestro_medidor
{
    public int maestro_medidor_id { get; set; }

    public string maestro_medidor_numero { get; set; } = null!;

    public string? maestro_medidor_marca { get; set; }

    public DateTime? maestro_medidor_fecha_instala { get; set; }

    public decimal? maestro_medidor_diametro { get; set; }

    public string? maestro_medidor_empleado { get; set; }

    public string? maestro_medidor_acueducto { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<cliente_detalle> cliente_detalles { get; set; } = new List<cliente_detalle>();
}
