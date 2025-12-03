using System;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class bnc_kardex_cuenta
{
    public int cod_banco { get; set; }

    public int cod_cuenta { get; set; }

    public char? conciliada { get; set; }

    public string? correlativo { get; set; }

    public short? disas_tran_ant { get; set; }

    public DateTime? fecha_creacion { get; set; }

    public DateTime? fecha_transaccion { get; set; }

    public double monto { get; set; }

    public double? monto_dolares { get; set; }

    public string? num_cheque { get; set; }

    public string? observaciones { get; set; }

    public char? pda { get; set; }

    public string? referencia1 { get; set; }

    public string? referencia2 { get; set; }

    public string? referencia_afecta { get; set; }

    public double saldo { get; set; }

    public double saldo_ant { get; set; }

    public double? saldo_dol { get; set; }

    public double? saldo_dol_ant { get; set; }

    public double? suma_balance { get; set; }

    public double? tasa { get; set; }

    public string? tipo_transacion { get; set; }

    public string? tipo_transacion2 { get; set; }

    public char? ultima_trn { get; set; }

    public string? usuario_creo { get; set; }

    public Guid? rowid { get; set; }
}
