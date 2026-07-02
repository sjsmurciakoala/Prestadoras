using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class Proyecto
{
    public int ide { get; set; }

    public string? empre { get; set; }

    public int? ano { get; set; }

    public string? codigo { get; set; }

    public string? descripcion { get; set; }

    public string? lugar { get; set; }

    public string? ubicacion { get; set; }

    public decimal? aprobado { get; set; }

    public string? supervisor { get; set; }

    public string? ejectutor { get; set; }

    public string? presupuesto { get; set; }

    public DateTime? fecha1 { get; set; }

    public DateTime? fecha2 { get; set; }

    public string? fuente_financiamiento { get; set; }

    public decimal? ampliado { get; set; }

    public decimal? pagado { get; set; }

    public string? fondo { get; set; }
}
