using SIAD.Core.DTOs.Configuracion;
using SIAD.Services.Almacen;

namespace SIAD.Tests.Infrastructure;

/// <summary>
/// No-op de <see cref="IAlertasStockNotificador"/> para los tests que construyen servicios de salida
/// (movimiento, descargo) y no verifican el envío de correo.
/// </summary>
public sealed class AlertasStockNotificadorNoop : IAlertasStockNotificador
{
    public Task<CorreoEnvioResultado> EnviarResumenAsync(string motivo, CancellationToken ct = default)
        => Task.FromResult(CorreoEnvioResultado.Skip("noop"));

    public Task<CorreoEnvioResultado> EnviarCrucesAsync(
        IReadOnlyCollection<(int articuloId, int bodegaId)> pares, string documento, CancellationToken ct = default)
        => Task.FromResult(CorreoEnvioResultado.Skip("noop"));
}
