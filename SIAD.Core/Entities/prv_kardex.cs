using System;
using System.Collections;
using System.Collections.Generic;

namespace SIAD.Core.Entities;

public partial class prv_kardex
{
    public string? cod_proveedor { get; set; }

    public string correlativo { get; set; } = null!;

    public string? cuenta_debitar { get; set; }

    public short? dias_trn_ant { get; set; }

    public DateTime? fecha_creacion { get; set; }

    public DateTime fecha_transaccion { get; set; }

    public DateTime? fec_vencimiento { get; set; }

    public double monto { get; set; }

    public double? monto_dolares { get; set; }

    public string? num_cheque { get; set; }

    public string? observaciones { get; set; }

    public string? pda { get; set; }

    public string referencia1 { get; set; } = null!;

    public string? referencia2 { get; set; }

    public string? referencia_afecta { get; set; }

    public double? saldo { get; set; }

    public double? saldo_anterior { get; set; }

    public double? saldo_dolares { get; set; }

    public BitArray? status_pago { get; set; }

    public double? suma_balance { get; set; }

    public string tipo_transaccion { get; set; } = null!;

    public string? tipo_transaccion2 { get; set; }

    public string? ultima_trn { get; set; }

    public string usuario_creo { get; set; } = null!;

    public string rowid { get; set; } = null!;

    public string? correlativo_dei { get; set; }

    public string? cai { get; set; }

    public int? cod_correlativo_dei { get; set; }

    public string? nombre_proveedor_p { get; set; }

    public double? saldo_anterior_dol { get; set; }

    public string? cuenta_acreditar { get; set; }
}
