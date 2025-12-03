using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class abogado
{
    public int abogado_id { get; set; }

    public string abogado_codigo { get; set; } = null!;

    public string abogado_nombrecorto { get; set; } = null!;

    public string? abogado_nombrelargo { get; set; }

    public string? abogado_telefono { get; set; }

    public bool estado { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariomodificacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public string? codcuenta { get; set; }
}
