using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ban_movimiento_transito
{
    public long movimiento_transito_id { get; set; }

    public long company_id { get; set; }

    public long banco_cuenta_id { get; set; }

    public long? movimiento_id { get; set; }

    public DateTime fecha { get; set; }

    public char aod { get; set; }

    public int no_ope { get; set; }

    public int no_conc { get; set; }

    public string? c_refer { get; set; }

    public string? cod_usua { get; set; }

    public DateTime fecha_lib { get; set; }

    public string? cod_bene { get; set; }

    public int tdc { get; set; }

    public int cdcd { get; set; }

    public string? descripcion { get; set; }

    public string? documento { get; set; }

    public string? comentario1 { get; set; }

    public string? comentario2 { get; set; }

    public string? comentario3 { get; set; }

    public string obcp { get; set; } = null!;

    public string? origen { get; set; }

    public int estado { get; set; }

    public decimal monto { get; set; }

    public decimal mto_db { get; set; }

    public decimal mto_cr { get; set; }

    public decimal monto1 { get; set; }

    public decimal monto2 { get; set; }

    public decimal saldo { get; set; }

    public DateTime created_at { get; set; }

    public string created_by { get; set; } = null!;

    public DateTime? updated_at { get; set; }

    public string? updated_by { get; set; }

    public virtual ban_cuenta banco_cuenta { get; set; } = null!;

    public virtual cfg_company company { get; set; } = null!;

    public virtual ban_movimiento? movimiento { get; set; }
}
