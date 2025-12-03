using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class pagos_banco
{
    public char? idreg { get; set; }

    public string? cliente_clave { get; set; }

    public string? rtn { get; set; }

    public decimal? recibo { get; set; }

    public decimal? montop { get; set; }

    public DateOnly? fechap { get; set; }

    public string? referencia { get; set; }

    public string? banco { get; set; }

    public string? sucursal { get; set; }

    public string? agencia { get; set; }

    public string? cajero { get; set; }

    public string? terminal { get; set; }

    public TimeOnly? horap { get; set; }
}
