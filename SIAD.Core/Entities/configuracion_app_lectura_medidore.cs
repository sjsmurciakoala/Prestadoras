using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class configuracion_app_lectura_medidore
{
    public int ide { get; set; }

    public string? descripcion { get; set; }

    public decimal? valor_numeros { get; set; }

    public string? valor_letras { get; set; }
}
