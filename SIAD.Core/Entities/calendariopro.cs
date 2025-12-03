using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class calendariopro
{
    public int ide { get; set; }

    public int? ano { get; set; }

    public int? mes { get; set; }

    public string? ciclo { get; set; }

    public DateOnly? fechaal { get; set; }

    public DateOnly? fechalec { get; set; }

    public DateOnly? fechafac { get; set; }

    public DateOnly? fecharefac { get; set; }

    public DateOnly? fechavence { get; set; }

    public int? diasvence { get; set; }

    public DateOnly? fechafac2 { get; set; }

    public DateOnly? fechavence2 { get; set; }
}
