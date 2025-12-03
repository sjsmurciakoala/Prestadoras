using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class tarifas_catalogo
{
    public int tarifa_catalogo_id { get; set; }

    public string nombre { get; set; } = null!;

    public decimal? precio_base { get; set; }

    public decimal? cargo_fijo { get; set; }

    public string? descripcion { get; set; }

    public bool activo { get; set; }

    public DateTime? fecha_creacion { get; set; }

    public DateTime? fecha_modificacion { get; set; }

    public virtual ICollection<configuracion_tasa> configuracion_tasas { get; set; } = new List<configuracion_tasa>();
}
