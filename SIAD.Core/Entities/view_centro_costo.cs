using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class view_centro_costo
{
    public string? codigo_costo { get; set; }

    public string? actividad { get; set; }

    public string? programa { get; set; }

    public string? objeto_gasto { get; set; }

    public string? contable { get; set; }
}
