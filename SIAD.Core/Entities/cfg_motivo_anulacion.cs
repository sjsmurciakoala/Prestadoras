using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cfg_motivo_anulacion
{
    public short motivo_anulacion_id { get; set; }

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public bool aplica_factura { get; set; }

    public bool aplica_recibo { get; set; }

    public bool activo { get; set; }

    public virtual ICollection<factura> facturas { get; set; } = new List<factura>();
}
