using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ajuste
{
    public int documento { get; set; }

    public DateOnly? fecha { get; set; }

    public string? estado { get; set; }

    public string? observacion { get; set; }

    public decimal? total { get; set; }

    public int? motivo { get; set; }

    public int? tipo_nota { get; set; }

    public decimal? saldo { get; set; }

    public string? periodo { get; set; }

    public decimal? lectura { get; set; }

    public string? usuario { get; set; }

    public string? cliente_clave { get; set; }

    public string? correlativo { get; set; }
}
