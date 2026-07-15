using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Facturacion;

namespace SIAD.Services.Facturacion;

/// <summary>
/// Calendario de facturación por empresa (calendariopro): fechas de lectura,
/// facturación y vencimiento de cada año/mes/ciclo. Lo leen sp_lectura_v3 y
/// sp_adm_calcular_factura_lectura para fechavence/plazo de la factura.
/// Fase A del plan apertura-ciclo-único (2026-07-14).
/// </summary>
public interface ICalendarioFacturacionService
{
    /// <summary>Años con calendario cargado, descendente.</summary>
    Task<IReadOnlyList<int>> ListarAniosAsync(long companyId, CancellationToken ct = default);

    /// <summary>Todas las filas del año, ordenadas por mes y ciclo.</summary>
    Task<CalendarioAnioDto> ObtenerAnioAsync(long companyId, int anio, CancellationToken ct = default);

    /// <summary>
    /// Persiste el calendario del año: upsert de las filas entrantes y borra
    /// las del año que ya no vienen. Devuelve el año recargado.
    /// </summary>
    Task<CalendarioAnioDto> GuardarAnioAsync(long companyId, int anio, List<CalendarioCicloDto> filas,
        string usuario, CancellationToken ct = default);

    /// <summary>
    /// Copia el calendario de un año a otro desplazando las fechas al año
    /// destino (mismo día/mes; 29-feb cae a 28-feb). Falla si el destino ya
    /// tiene filas. Devuelve el año destino recargado.
    /// </summary>
    Task<CalendarioAnioDto> CopiarAnioAsync(long companyId, int anioOrigen, int anioDestino,
        string usuario, CancellationToken ct = default);
}
