using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

/// <summary>
/// Letra: Código de categorización de clientes (ej. A, B, C, etc.)
/// </summary>
public partial class letra
{
    public string letras { get; set; } = null!;

    public decimal? num { get; set; }

    public DateTime? fechacreacion { get; set; }

    public string? usuariocreacion { get; set; }

    public DateTime? fechamodificacion { get; set; }

    public string? usuariomodificacion { get; set; }
}
