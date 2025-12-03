using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_partidas_dtl
{
    public decimal? cargos { get; set; }

    public string? cod_centrocosto { get; set; }

    public string? cod_cliente { get; set; }

    public string? cod_cuenta { get; set; }

    public int? cod_empresa { get; set; }

    public string? cod_marcagrupo { get; set; }

    public int? cod_partida { get; set; }

    public string? comprobante { get; set; }

    public string? concepto { get; set; }

    public int? correlativo { get; set; }

    public decimal? creditos { get; set; }

    public int? tasacambio { get; set; }

    public Guid? rowid { get; set; }
}
