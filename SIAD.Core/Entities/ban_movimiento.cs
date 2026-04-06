using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_movimiento
{
    public long movimiento_id { get; set; }

    public long company_id { get; set; }

    public long banco_cuenta_id { get; set; }

    public string tipo { get; set; } = null!;

    public DateOnly fecha_movimiento { get; set; }

    public string currency_code { get; set; } = null!;

    public decimal exchange_rate { get; set; }

    public decimal monto { get; set; }

    public decimal monto_local { get; set; }

    public string? descripcion { get; set; }

    public string? referencia { get; set; }

    public string? origen_modulo { get; set; }

    public long? origen_documento_id { get; set; }

    public long? con_partida_hdr_id { get; set; }

    public string estado { get; set; } = null!;

    public bool conciliado { get; set; }

    public DateOnly? fecha_conciliacion { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public char aod { get; set; }

    public int no_ope { get; set; }

    public int nro_comp { get; set; }

    public int ope_rel { get; set; }

    public string? c_refer { get; set; }

    public string? cod_esta { get; set; }

    public string? cod_usua { get; set; }

    public string? cod_sucu { get; set; }

    public string? cod_oper { get; set; }

    public DateTime? fecha_lib { get; set; }

    public int tip_ben { get; set; }

    public string? cod_bene { get; set; }

    public int tdc { get; set; }

    public int cdcd { get; set; }

    public string? documento { get; set; }

    public string? comentario1 { get; set; }

    public string? comentario2 { get; set; }

    public string? comentario3 { get; set; }

    public string? memo { get; set; }

    public string? obcp { get; set; }

    public int nro_ppal { get; set; }

    public string? origen_legacy { get; set; }

    public int estado_legacy { get; set; }

    public DateTime? fec_conc { get; set; }

    public int no_conc { get; set; }

    public decimal mto_db { get; set; }

    public decimal mto_cr { get; set; }

    public int endosable { get; set; }

    public int tipo_ope { get; set; }

    public string? cta_idb { get; set; }

    public string? d_cta_idb { get; set; }

    public decimal mto_idb { get; set; }

    public int consolidado { get; set; }

    public DateTime? f_consolidado { get; set; }

    public decimal nro_egreso { get; set; }

    public decimal mto_debito { get; set; }

    public string? dcto_origen { get; set; }

    public decimal mto_origen { get; set; }

    public string? bene_origen { get; set; }

    public decimal monto1 { get; set; }

    public decimal monto2 { get; set; }

    public decimal mto_deb { get; set; }

    public string? dcto_ori { get; set; }

    public string? bene_ori { get; set; }

    public decimal mto_ori { get; set; }

    public decimal saldo { get; set; }

    public virtual ICollection<ban_movimiento_detalle> ban_movimiento_detalle { get; set; } = new List<ban_movimiento_detalle>();

    public virtual ICollection<ban_movimiento_transito> ban_movimiento_transito { get; set; } = new List<ban_movimiento_transito>();

    public virtual ban_cuenta banco_cuenta { get; set; } = null!;

    public virtual cfg_company company { get; set; } = null!;
}

