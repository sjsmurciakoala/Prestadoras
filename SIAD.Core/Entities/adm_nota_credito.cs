using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class adm_nota_credito
{
    public long nota_credito_id { get; set; }

    public long company_id { get; set; }

    public string establecimiento_codigo { get; set; } = "000";

    public short tipo_documento_fiscal_id { get; set; }

    public string numero_documento { get; set; } = null!;

    public long cai_id { get; set; }

    public long correlativo { get; set; }

    public DateTime fecha_emision { get; set; }

    public DateOnly fecha_limite_cai { get; set; }

    public string? leyenda_cai_rango { get; set; }

    public string rtn_emisor { get; set; } = null!;

    public string razon_social_emisor { get; set; } = null!;

    public string? direccion_emisor { get; set; }

    public long cliente_id { get; set; }

    public string? rtn_receptor { get; set; }

    public string razon_social_receptor { get; set; } = null!;

    public string? direccion_receptor { get; set; }

    public int factura_origen_id { get; set; }

    public string factura_origen_numero { get; set; } = null!;

    public DateOnly factura_origen_fecha { get; set; }

    public string? factura_origen_cai { get; set; }

    public short motivo_anulacion_id { get; set; }

    public string? motivo_detalle { get; set; }

    public decimal monto_disminuir { get; set; }

    public decimal isv_disminuir { get; set; }

    public decimal total_nota { get; set; }

    public bool anula_factura_origen { get; set; }

    public short estado_id { get; set; }

    public string usuario_emisor { get; set; } = null!;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ICollection<adm_nota_credito_detalle> detalles { get; set; } = new List<adm_nota_credito_detalle>();
}
