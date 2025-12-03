using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class orden_trabajo_adjunto
{
    public int id { get; set; }

    public byte[]? adjunto { get; set; }

    public string? nombre { get; set; }

    public string? tipo { get; set; }

    public string? latitud { get; set; }

    public string? longitud { get; set; }

    public string? numeroorden { get; set; }

    public DateTime? fechainicio { get; set; }

    public DateTime? fechafin { get; set; }

    public DateTime? fechaobtenerordenes { get; set; }
}
