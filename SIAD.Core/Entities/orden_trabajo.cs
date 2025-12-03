using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class orden_trabajo
{
    public int orden_id { get; set; }

    public int orden_numero { get; set; }

    public string maestro_cliente_clave { get; set; } = null!;

    public string concepto { get; set; } = null!;

    public string estado { get; set; } = null!;

    public DateOnly fecha { get; set; }

    public DateTime fecha_creacion { get; set; }

    public string? informe { get; set; }

    public int ano { get; set; }

    public int mes { get; set; }

    public decimal? saldo { get; set; }

    public string? usuario { get; set; }

    public string? tipo { get; set; }

    public string? empleado { get; set; }

    public string? personas { get; set; }
}
