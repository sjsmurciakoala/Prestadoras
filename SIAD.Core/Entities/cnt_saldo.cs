using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class cnt_saldo
{
    public double cargos { get; set; }

    public string cod_cuenta { get; set; } = null!;

    public int cod_empresa { get; set; }

    public double creditos { get; set; }

    public DateOnly? fecha_cierre { get; set; }

    public DateTime hora_cierre { get; set; }

    public double saldo_actual { get; set; }

    public double saldo_anterior { get; set; }

    public DateOnly? ult_fecha_modificada { get; set; }

    public string? ult_usuario { get; set; }

    public Guid? rowid { get; set; }
}
