using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class historialme
{
    public decimal ano { get; set; }

    public decimal mes { get; set; }

    public string ciclo { get; set; } = null!;

    public string? ruta { get; set; }

    public DateTime? fecha { get; set; }

    public decimal? sep { get; set; }

    public decimal? sep2 { get; set; }

    public DateOnly? fechacierre { get; set; }

    public string? usuarioapertura { get; set; }

    public string? usuariocierre { get; set; }

    public char? cerrado { get; set; }

    public char? cerrarperiodo { get; set; }

    public DateTime? fechaperiodo { get; set; }

    public string? _2_Sep { get; set; }
}
