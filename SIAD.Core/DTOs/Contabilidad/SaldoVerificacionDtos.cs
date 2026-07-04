using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// Divergencia entre el caché oficial de saldos (con_saldo_cuenta) y el
/// cálculo vivo desde el libro (con_partida_dtl), por período y cuenta
/// (fn_con_verificar_saldo_cuenta, plan 2026-07-02 F6).
/// Tipos: SOLO_CACHE, SOLO_LIBRO, MONTOS, CONTEOS.
/// </summary>
public sealed class SaldoDivergenciaDto
{
    public long PeriodId { get; set; }
    public string? PeriodoCode { get; set; }
    public string CodigoCuenta { get; set; } = string.Empty;
    public string TipoDivergencia { get; set; } = string.Empty;
    public decimal? DebitosCache { get; set; }
    public decimal? DebitosLibro { get; set; }
    public decimal? CreditosCache { get; set; }
    public decimal? CreditosLibro { get; set; }
    public int? CantidadDebitosCache { get; set; }
    public int? CantidadDebitosLibro { get; set; }
    public int? CantidadCreditosCache { get; set; }
    public int? CantidadCreditosLibro { get; set; }
}

/// <summary>
/// Resultado de la reconciliación caché vs libro de una empresa.
/// 0 divergencias = consistente; divergencias &gt; 0 = correr
/// sp_con_reconstruir_saldo_cuenta en ventana de mantenimiento (regla F6).
/// </summary>
public sealed class SaldoVerificacionResultDto
{
    public DateTime VerificadoEn { get; set; }
    public int TotalDivergencias { get; set; }
    public int SoloCache { get; set; }
    public int SoloLibro { get; set; }
    public int Montos { get; set; }
    public int Conteos { get; set; }
    public bool Consistente => TotalDivergencias == 0;
    public IReadOnlyList<SaldoDivergenciaDto> Divergencias { get; set; } = [];
}
