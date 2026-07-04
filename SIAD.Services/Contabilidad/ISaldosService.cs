using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Consulta y reconciliación del caché oficial de saldos (con_saldo_cuenta).
/// D1: la ESCRITURA del caché es exclusiva del motor único en BD
/// (sp_con_postear_poliza / sp_con_revertir_poliza vía
/// sp_con_actualizar_saldos_por_poliza) — este servicio es solo lectura.
/// La reconstrucción (sp_con_reconstruir_saldo_cuenta, F6) se corre por SQL
/// en ventana de mantenimiento, a propósito sin endpoint.
/// </summary>
public interface ISaldosService
{
    /// <summary>Saldo cacheado (débitos, créditos) de una cuenta en un período.</summary>
    Task<(decimal debitos, decimal creditos)> ObtenerSaldoAsync(
        long companyId,
        long periodId,
        long accountId,
        long? costCenterId = null,
        CancellationToken ct = default
    );

    /// <summary>
    /// Reconciliación caché vs libro (fn_con_verificar_saldo_cuenta, F6):
    /// divergencias por período/cuenta. 0 divergencias = consistente.
    /// </summary>
    Task<SaldoVerificacionResultDto> VerificarAsync(
        long companyId,
        long? periodId = null,
        CancellationToken ct = default
    );
}
