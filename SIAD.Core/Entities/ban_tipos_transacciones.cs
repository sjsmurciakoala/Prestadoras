using System;

namespace SIAD.Core.Entities;

public partial class ban_tipos_transacciones
{
    public long ban_tipo_transaccion_id { get; set; }

    public long company_id { get; set; }

    public string tipo_transaccion { get; set; } = null!;

    public string cod_tipopartida { get; set; } = null!;

    public string correlativo { get; set; } = null!;

    public long? cod_centrocosto { get; set; }

    public string? cuenta_contable { get; set; }

    public string? destino { get; set; }

    public string nombre { get; set; } = null!;

    public string? observaciones { get; set; }

    public string entra_sale { get; set; } = null!;

    public string del_sistema { get; set; } = null!;

    public string? emite_cheque { get; set; }

    public string? pad { get; set; }

    public string? pda { get; set; }

    public string? rel_empleados { get; set; }

    public string? trn_prestamo { get; set; }

    public short? filtro { get; set; }

    public bool cuenta_alterna { get; set; }

    public string estado { get; set; } = null!;

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual con_centro_costo? cost_center { get; set; }
}
