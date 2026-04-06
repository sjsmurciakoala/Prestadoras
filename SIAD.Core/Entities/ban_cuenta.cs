using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_cuenta
{
    public long banco_cuenta_id { get; set; }

    public long company_id { get; set; }

    public string code { get; set; } = null!;

    public string nombre { get; set; } = null!;

    public string? banco_nombre { get; set; }

    public long? branch_id { get; set; }

    public string tipo { get; set; } = null!;

    public string currency_code { get; set; } = null!;

    public string numero_cuenta { get; set; } = null!;

    public decimal saldo_inicial { get; set; }

    public DateOnly? fecha_saldo { get; set; }

    public string estado { get; set; } = null!;

    public bool allow_reconciliation { get; set; }

    public long? cont_account_id { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public long? ban_banco_id { get; set; }

    public long? ban_cta_id { get; set; }

    public int tdc { get; set; }

    public decimal saldo_actual { get; set; }

    public decimal saldo_c1 { get; set; }

    public DateTime? fecha_c1 { get; set; }

    public decimal saldo_c2 { get; set; }

    public DateTime? fecha_c2 { get; set; }

    public int proxima_conciliacion { get; set; }

    public int inversion_cheque { get; set; }

    public int idb { get; set; }

    public decimal pdb { get; set; }

    public string? cta_debito { get; set; }

    public int proximo_nddb { get; set; }

    public int? cta_conc { get; set; } = null!;

    public int r_transf { get; set; }

    public int meses_h { get; set; }

    public int v_no_ch { get; set; }

    public int v_no_dp { get; set; }

    public int v_no_nc { get; set; }

    public int v_no_nd { get; set; }

    public decimal proximo_cheque { get; set; }

    public int n_comp0 { get; set; }

    public int n_comp1 { get; set; }

    public int n_comp2 { get; set; }

    public int n_comp3 { get; set; }

    public int n_comp4 { get; set; }

    public int n_comp5 { get; set; }

    public bool activo { get; set; }

    public virtual ban_banco? ban_banco { get; set; }

    public virtual ban_cta? ban_cta { get; set; }

    public virtual ICollection<ban_movimiento> ban_movimiento { get; set; } = new List<ban_movimiento>();

    public virtual ICollection<ban_movimiento_transito> ban_movimiento_transito { get; set; } = new List<ban_movimiento_transito>();

    public virtual cfg_company company { get; set; } = null!;
}
