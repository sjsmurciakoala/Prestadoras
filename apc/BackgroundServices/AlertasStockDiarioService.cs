using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace apc.BackgroundServices;

/// <summary>
/// Envía el resumen diario de alertas de stock a una hora configurable
/// (<c>Almacen:AlertasStock:HoraDiaria</c>, formato <c>HH:mm</c>; por defecto 07:00). Interruptor
/// <c>Almacen:AlertasStock:Activo</c> (por defecto true).
/// <para>
/// ⚠️ <b>Despliegue en IIS:</b> el App Pool debe quedar en <c>AlwaysRunning</c> con idle time-out 0,
/// o la app se duerme y el temporizador no corre. La lógica vive en <see cref="AlertasStockBarrido"/>
/// para poder dispararla también desde una Tarea de Windows si se prefiere.
/// </para>
/// </summary>
public sealed class AlertasStockDiarioService : BackgroundService
{
    private readonly AlertasStockBarrido _barrido;
    private readonly IConfiguration _config;
    private readonly ILogger<AlertasStockDiarioService> _log;

    public AlertasStockDiarioService(AlertasStockBarrido barrido, IConfiguration config, ILogger<AlertasStockDiarioService> log)
    {
        _barrido = barrido;
        _config = config;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var espera = TiempoHastaProximaCorrida();

            try
            {
                await Task.Delay(espera, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            if (!_config.GetValue("Almacen:AlertasStock:Activo", true))
            {
                continue;
            }

            try
            {
                await _barrido.EjecutarAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Fallo el barrido diario de alertas de stock.");
            }
        }
    }

    private TimeSpan TiempoHastaProximaCorrida()
    {
        var horaTexto = _config.GetValue<string>("Almacen:AlertasStock:HoraDiaria");
        if (!TimeSpan.TryParse(horaTexto, out var hora))
        {
            hora = new TimeSpan(7, 0, 0);
        }

        var ahora = DateTime.Now;
        var hoyALaHora = ahora.Date + hora;
        var proxima = hoyALaHora > ahora ? hoyALaHora : hoyALaHora.AddDays(1);
        return proxima - ahora;
    }
}
