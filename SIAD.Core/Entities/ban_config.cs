using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_config
{
    public long ban_config_id { get; set; }

    public long company_id { get; set; }

    public decimal max_cheque { get; set; }

    public int dias_d1 { get; set; }

    public int dias_d2 { get; set; }

    public int dias_d3 { get; set; }

    public int i_cheque_ne { get; set; }

    public int i_c_egreso { get; set; }

    public int prx_c_egreso { get; set; }

    public int prx_deposito { get; set; }

    public int prx_n_debito { get; set; }

    public int prx_n_credito { get; set; }

    public decimal p_deb_ban { get; set; }

    public int meses_h { get; set; }

    public string? st_dta { get; set; }

    public int alertar_nd { get; set; }

    public int m_ope_conc { get; set; }

    public bool consolidado { get; set; }

    public string? cuenta_mayor { get; set; }

    public string? dir_contab { get; set; }

    public string? dir_dta_cont { get; set; }

    public int cc_tipo { get; set; }

    public string? cc_descrip { get; set; }

    public int cc_ssw { get; set; }

    public string? cc_server { get; set; }

    public string? cc_db { get; set; }

    public string? cc_user { get; set; }

    public string? cc_pwd { get; set; }

    public int cc_prefix { get; set; }

    public int nro_cxb { get; set; }

    public int a_ctas0 { get; set; }

    public int a_ctas1 { get; set; }

    public int a_ctas2 { get; set; }

    public int a_ctas3 { get; set; }

    public int a_ctas4 { get; set; }

    public int a_ctas5 { get; set; }

    public int n_ope1 { get; set; }

    public int n_ope2 { get; set; }

    public int n_ope3 { get; set; }

    public int n_ope4 { get; set; }

    public int n_ope5 { get; set; }

    public int n_ope6 { get; set; }

    public int n_ope7 { get; set; }

    public int n_ope8 { get; set; }

    public int n_ope9 { get; set; }

    public int n_ope10 { get; set; }

    public string? cta_aux1 { get; set; }

    public string? cta_aux2 { get; set; }

    public string? cta_aux3 { get; set; }

    public string? cod_sucu { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual cfg_company company { get; set; } = null!;
}
