using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ordent_mate
{
    public int id { get; set; }

    public string? cuenta { get; set; }

    public int? numero { get; set; }

    public string? codproduc { get; set; }

    public string? descripcion { get; set; }

    public int? cantidad { get; set; }

    public DateTime? fecha { get; set; }
}
