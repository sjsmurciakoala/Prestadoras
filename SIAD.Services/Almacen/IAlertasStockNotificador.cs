using SIAD.Core.DTOs.Configuracion;

namespace SIAD.Services.Almacen;

/// <summary>
/// Empuja las alertas de stock (bajo mínimo / sin stock / negativa) por correo al área ALMACÉN.
/// Reutiliza la detección existente (<c>ArticulosService.GetAlertasStockAsync</c>); no recalcula
/// reglas. Empresa en contexto (tenant actual).
/// </summary>
public interface IAlertasStockNotificador
{
    /// <summary>
    /// Envía el RESUMEN completo de alertas de la empresa actual (para el resumen diario o un
    /// disparo manual). Si no hay alertas, devuelve <c>Omitido</c> (no envía).
    /// </summary>
    Task<CorreoEnvioResultado> EnviarResumenAsync(string motivo, CancellationToken ct = default);

    /// <summary>
    /// Envía un aviso ENFOCADO de los pares (artículo, bodega) que acaban de cruzar a alerta tras un
    /// movimiento (evento). Filtra a los que siguen en alerta al leer el estado actual. Si ninguno
    /// sigue en alerta o la lista viene vacía, devuelve <c>Omitido</c>.
    /// </summary>
    Task<CorreoEnvioResultado> EnviarCrucesAsync(
        IReadOnlyCollection<(int articuloId, int bodegaId)> pares, string documento, CancellationToken ct = default);
}
