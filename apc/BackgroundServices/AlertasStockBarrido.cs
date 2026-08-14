using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SIAD.Core.Constants;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Almacen;

namespace apc.BackgroundServices;

/// <summary>
/// Barrido de alertas de stock: por cada empresa con el área ALMACÉN activa, envía el resumen por
/// correo. Reutilizable — lo llama el <see cref="AlertasStockDiarioService"/>, y podría llamarlo un
/// endpoint / Tarea de Windows sin duplicar lógica.
/// <para>
/// Corre sin sesión HTTP, así que fija el tenant por empresa con <see cref="TenantOverride"/> y crea
/// un scope de DI por empresa: así reutiliza el mismo camino que un request (alertas + notificador),
/// resueltos ya con la empresa correcta.
/// </para>
/// </summary>
public sealed class AlertasStockBarrido
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AlertasStockBarrido> _log;

    public AlertasStockBarrido(IServiceScopeFactory scopes, ILogger<AlertasStockBarrido> log)
    {
        _scopes = scopes;
        _log = log;
    }

    public async Task EjecutarAsync(CancellationToken ct = default)
    {
        var empresas = await EmpresasConAlmacenActivoAsync(ct);
        _log.LogInformation("Barrido de alertas de stock: {N} empresa(s) con área ALMACÉN activa.", empresas.Count);

        foreach (var companyId in empresas)
        {
            if (ct.IsCancellationRequested) break;
            try
            {
                // Tenant fijado ANTES de crear el scope: el DbContext se construye ya con esta empresa.
                using (TenantOverride.Begin(companyId))
                using (var scope = _scopes.CreateScope())
                {
                    var notificador = scope.ServiceProvider.GetRequiredService<IAlertasStockNotificador>();
                    var r = await notificador.EnviarResumenAsync(
                        "Resumen diario de artículos que requieren atención:", ct);
                    _log.LogInformation("Empresa {Company}: resumen de alertas {Estado}.",
                        companyId, r.Exito ? "enviado" : r.Omitido ? "omitido" : "fallido");
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Fallo el resumen de alertas de stock para la empresa {Company}.", companyId);
            }
        }
    }

    private async Task<IReadOnlyList<long>> EmpresasConAlmacenActivoAsync(CancellationToken ct)
    {
        // Enumeración CROSS-TENANT (sin empresa en contexto): IgnoreQueryFilters a propósito. El envío
        // se auto-gatea después (la conexión inactiva → Omitido), así que basta el área activa.
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SiadDbContext>();
        return await db.cfg_notificacions.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.tipo == TipoNotificacion.Almacen && n.activo)
            .Select(n => n.company_id)
            .Distinct()
            .ToListAsync(ct);
    }
}
