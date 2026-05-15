using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cfg_tipo_documento_fiscal
{
    public short tipo_documento_fiscal_id { get; set; }

    public string codigo { get; set; } = null!;

    public string descripcion { get; set; } = null!;

    public bool es_comprobante_fiscal { get; set; }

    public bool es_documento_complementario { get; set; }

    public bool requiere_factura_origen { get; set; }

    public bool activo { get; set; }

    public virtual ICollection<adm_cai_facturacion> adm_cai_facturacions { get; set; } = new List<adm_cai_facturacion>();

    public virtual ICollection<factura> facturas { get; set; } = new List<factura>();
}
