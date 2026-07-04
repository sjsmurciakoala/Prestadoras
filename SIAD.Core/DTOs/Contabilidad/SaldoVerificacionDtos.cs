using System;
using System.Collections.Generic;

namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// Tipos de divergencia que devuelve fn_con_verificar_saldo_cuenta (F6).
/// Discriminador de diagnóstico (no es un estado persistido).
/// </summary>
public static class SaldoDivergenciaTipos
{
    public const string SoloCache = "SOLO_CACHE";
    public const string SoloLibro = "SOLO_LIBRO";
    public const string Montos = "MONTOS";
    public const string Conteos = "CONTEOS";

    /// <summary>
    /// Póliza posteada con fecha fuera del rango de su período: rompe la
    /// equivalencia caché=vivo del balance híbrido. Corregir la póliza,
    /// no el caché.
    /// </summary>
    public const string FechaFueraPeriodo = "FECHA_FUERA_PERIODO";
}

/// <summary>
/// Divergencia entre el caché oficial de saldos (con_saldo_cuenta) y el
/// cálculo vivo desde el libro (con_partida_dtl), por período y cuenta
/// (fn_con_verificar_saldo_cuenta, plan 2026-07-02 F6). Tipos en
/// <see cref="SaldoDivergenciaTipos"/>.
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
/// sp_con_reconstruir_saldo_cuenta en ventana de mantenimiento (regla F6);
/// FechasFueraPeriodo &gt; 0 = corregir period_id/poliza_date de esas pólizas.
/// Los contadores se calculan sobre el TOTAL aunque
/// <see cref="Divergencias"/> venga truncada (<see cref="DetalleTruncado"/>).
/// </summary>
public sealed class SaldoVerificacionResultDto
{
    public DateTime VerificadoEn { get; set; }
    public int TotalDivergencias { get; set; }
    public int SoloCache { get; set; }
    public int SoloLibro { get; set; }
    public int Montos { get; set; }
    public int Conteos { get; set; }
    public int FechasFueraPeriodo { get; set; }
    public bool Consistente => TotalDivergencias == 0;
    public bool DetalleTruncado { get; set; }
    public IReadOnlyList<SaldoDivergenciaDto> Divergencias { get; set; } = [];
}
