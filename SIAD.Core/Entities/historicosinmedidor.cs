using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class historicosinmedidor
{
    public int ide { get; set; }

    public string? cuenta { get; set; }

    public int? ano { get; set; }

    public int? mes { get; set; }

    public string? numerofactura { get; set; }

    public int? correlativocai { get; set; }

    public int? idcai { get; set; }

    public DateTime? fecha { get; set; }

    public string? usuario { get; set; }
}
