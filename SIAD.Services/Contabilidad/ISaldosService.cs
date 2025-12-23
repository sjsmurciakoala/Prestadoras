namespace SIAD.Services.Contabilidad;

/// <summary>
/// Servicio para gestión de saldos de cuentas contables
/// Actualiza automáticamente con_saldo_cuenta cuando se registran/revierten pólizas
/// </summary>
public interface ISaldosService
{
    /// <summary>Actualizar saldos por líneas de póliza (sumar o restar)</summary>
    Task ActualizarSaldosPorPolizaAsync(
        long companyId,
        long polizaId,
        bool sumar,
        CancellationToken ct = default
    );

    /// <summary>Obtener saldo actual de una cuenta en un período</summary>
    Task<(decimal debitos, decimal creditos)> ObtenerSaldoAsync(
        long companyId,
        long periodId,
        long accountId,
        long? costCenterId = null,
        CancellationToken ct = default
    );

    /// <summary>Inicializar saldos de un período desde el anterior</summary>
    Task InicializarPeriodoAsync(
        long companyId,
        long nuevoPeriodId,
        long periodoAnteriorId,
        CancellationToken ct = default
    );
}
