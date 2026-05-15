using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class adm_cai_facturacion
{
    public long cai_id { get; set; }

    public long company_id { get; set; }

    public string codigo_cai { get; set; } = null!;

    public string prefijo_documento { get; set; } = null!;

    public string punto_emision { get; set; } = "001";

    public long rango_desde { get; set; }

    public long rango_hasta { get; set; }

    public DateOnly vigencia_desde { get; set; }

    public DateOnly? vigencia_hasta { get; set; }

    public string? observaciones { get; set; }

    public short status_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public short tipo_documento_fiscal_id { get; set; }

    public DateOnly fecha_limite_emision { get; set; }

    public string? leyenda_rango { get; set; }

    public virtual cfg_tipo_documento_fiscal tipo_documento_fiscal { get; set; } = null!;
}
