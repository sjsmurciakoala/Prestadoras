using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class ops_compromiso
{
    public int cod_empresa { get; set; }

    public int ano { get; set; }

    public string? orden { get; set; }

    public decimal? numero { get; set; }

    public string codigo { get; set; } = null!;

    public string? beneficiario { get; set; }

    public string? cod_programa { get; set; }

    public string? cod_actvidad { get; set; }

    public string? cod_gastos { get; set; }

    public decimal? compromiso { get; set; }

    public DateTime? fecha { get; set; }

    public DateTime? fechavence { get; set; }

    public string? concepto { get; set; }

    public decimal? pagos { get; set; }

    public DateTime? fechap { get; set; }

    public string? docu { get; set; }

    public string? codproy { get; set; }

    public string? fondo { get; set; }

    public decimal? paga { get; set; }

    public string? cta_contable { get; set; }

    public string? ctacobrar { get; set; }

    public int? ordenp { get; set; }

    public int? id { get; set; }

    public string? cod_proveedor { get; set; }

    public string? bor { get; set; }

    public string? aplicado { get; set; }

    public string? rtn { get; set; }
}
