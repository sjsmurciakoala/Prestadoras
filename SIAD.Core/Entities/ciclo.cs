using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ciclo
{
    public int ciclos_id { get; set; }

    public string ciclos_codigo { get; set; } = null!;

    public string ciclos_descripcioncorta { get; set; } = null!;

    public string ciclos_descripcionlarga { get; set; } = null!;

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public virtual ICollection<cliente_maestro> cliente_maestros { get; set; } = new List<cliente_maestro>();
}
