using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class presupuesto
{
    public int id_presupuesto { get; set; }

    public string centro_costo { get; set; } = null!;

    public double monto { get; set; }

    public DateTime fecha_creacion { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public string usuario_creo { get; set; } = null!;

    public string? usuario_modifico { get; set; }

    public string fondo { get; set; } = null!;

    public bool? estado { get; set; }

    public int? ano { get; set; }
}
