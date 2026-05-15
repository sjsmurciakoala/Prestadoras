using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class adm_nota_credito_detalle
{
    public long nota_credito_detalle_id { get; set; }

    public long nota_credito_id { get; set; }

    public long? servicio_id { get; set; }

    public string? servicio_codigo { get; set; }

    public string descripcion { get; set; } = null!;

    public decimal cantidad { get; set; }

    public decimal monto_unitario { get; set; }

    public decimal monto_total { get; set; }

    public decimal isv_monto { get; set; }

    public string? cuenta_contable_codigo { get; set; }

    public virtual adm_nota_credito nota_credito { get; set; } = null!;
}
