using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class transaccion_presupuesto
{
    public int ide { get; set; }

    public string? trpr_descripcion { get; set; }

    public string? trpr_tipo_transaccion { get; set; }

    public string? trpr_presupuesto_origen { get; set; }

    public DateTime? trpr_fecha { get; set; }

    public decimal? trpr_monto { get; set; }

    public decimal? trp_saldo { get; set; }

    public int? trpr_ano { get; set; }

    public string? trpr_destino { get; set; }

    public string? trpr_codigoproyecto { get; set; }

    public string? trpr_tipodestino { get; set; }

    public int? trpr_fondo_id { get; set; }
}
