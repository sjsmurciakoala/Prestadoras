using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_movimiento_detalle
{
    public long movimiento_detalle_id { get; set; }

    public long company_id { get; set; }

    public long movimiento_id { get; set; }

    public int linea_num { get; set; }

    public string? cod_cta { get; set; }

    public int es_transf { get; set; }

    public int es_cuenta { get; set; }

    public string? cod_usua { get; set; }

    public string? cod_sucu { get; set; }

    public string? cod_oper { get; set; }

    public string? cod_esta { get; set; }

    public int cdcd { get; set; }

    public int enc_ope { get; set; }

    public DateTime fecha { get; set; }

    public string? descripcion { get; set; }

    public string? origen { get; set; }

    public int estado { get; set; }

    public int dh { get; set; }

    public int n_mo { get; set; }

    public decimal base_tr { get; set; }

    public decimal monto { get; set; }

    public decimal mto_db { get; set; }

    public decimal mto_cr { get; set; }

    public int consolidado { get; set; }

    public DateTime? f_consolidado { get; set; }

    public int si_centro { get; set; }

    public int si_tercero { get; set; }

    public string? cod_cen_cto { get; set; }

    public string? cod_tercero { get; set; }

    public string? tercero { get; set; }

    public decimal? flujo_e { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual cfg_company company { get; set; } = null!;

    public virtual ban_movimiento movimiento { get; set; } = null!;
}
