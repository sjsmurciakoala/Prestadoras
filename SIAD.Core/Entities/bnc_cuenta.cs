using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class bnc_cuenta
{
    public int cod_banco { get; set; }

    public int cod_cuenta { get; set; }

    public int cod_empresa { get; set; }

    public string cuenta_contable { get; set; } = null!;

    public string? descripcion { get; set; }

    public bool? emite_cheques { get; set; }

    public string? numero_cheque { get; set; }

    public string? ruta_transito { get; set; }

    public double? saldo { get; set; }

    public double? saldo_conciliado { get; set; }

    public double? tasa_promedio { get; set; }

    public string tipo_cuenta { get; set; } = null!;

    public Guid? rowid { get; set; }

    public string? codigo { get; set; }
}
