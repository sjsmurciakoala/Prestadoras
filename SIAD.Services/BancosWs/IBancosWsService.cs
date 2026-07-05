using System;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.BancosWs;

namespace SIAD.Services.BancosWs;

/// <summary>
/// Servicio del WS bancario (plan 2026-07-02 F8, contrato SIMAFI congelado —
/// docs/f8-contrato-ws-bancario.md). La lógica de dinero vive en los SPs
/// sp_ban_ws_pagar / sp_ban_ws_reversar (Database/ddl_v3/20260704_ci_fase8_ws_bancario.sql);
/// este servicio es el puente Dapper + la consulta de saldos.
/// </summary>
public interface IBancosWsService
{
    /// <summary>Valida banco+llave contra ban_ws_credencial (global, resuelve el tenant).</summary>
    Task<BancosWsCredencialDto?> AutenticarAsync(string? banco, string? llave, CancellationToken ct = default);

    /// <summary>Consulta de saldos pendientes del abonado (tenant actual).</summary>
    Task<BancosWsConsultaDto> ConsultarAsync(string clave, CancellationToken ct = default);

    /// <summary>Pago del banco vía sp_ban_ws_pagar (idempotente por referencia).</summary>
    Task<BancosWsPagoResultDto> PagarAsync(BancosWsPagoRequestDto pago, long? bancoCuentaId, CancellationToken ct = default);

    /// <summary>Reversión por referencia vía sp_ban_ws_reversar.</summary>
    Task<BancosWsReversionResultDto> ReversarAsync(string referencia, string? cajero, CancellationToken ct = default);

    /// <summary>Semántica genkey de SrvAutorizacion: regenera la llave (40 hex) del banco. La llave NO se devuelve (contrato).</summary>
    Task<bool> GenerarLlaveAsync(string? banco, string? vigencia, CancellationToken ct = default);

    /// <summary>Vigencia informativa del banco: fecha, "permanente" (null en BD) o null si el banco no existe.</summary>
    Task<(bool Existe, DateOnly? Vigencia)> ObtenerVigenciaAsync(string? banco, CancellationToken ct = default);
}
