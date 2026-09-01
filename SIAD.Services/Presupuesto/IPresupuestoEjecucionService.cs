using SIAD.Core.DTOs.Presupuesto;

namespace SIAD.Services.Presupuesto;

/// <summary>
/// Consultas del control presupuestario y su interruptor. Todo es lectura sobre las vistas
/// <c>vw_pst_*</c>, salvo el guardado de <c>cfg_presupuesto_control</c>.
/// <para>
/// Antes de esto, <c>valor_real</c> era un número acumulado sin historia: nadie podía responder
/// «¿por qué esta cuenta está al 90%?». El kardex es lo que vuelve auditable el saldo.
/// </para>
/// </summary>
public interface IPresupuestoEjecucionService
{
    /// <summary>Ejecución por partida: presupuesto, comprometido, ejecutado, pagado y disponible.</summary>
    Task<IReadOnlyList<PresupuestoEjecucionItemDto>> ListarEjecucionAsync(
        PresupuestoEjecucionFilterDto? filtro, CancellationToken ct = default);

    /// <summary>Órdenes aprobadas que todavía retienen presupuesto comprometido.</summary>
    Task<IReadOnlyList<PresupuestoCompromisoPendienteDto>> ListarCompromisosPendientesAsync(
        PresupuestoCompromisoFilterDto? filtro, CancellationToken ct = default);

    /// <summary>
    /// Kardex de una partida: todos sus movimientos con los saldos antes y después. Permite
    /// reconstruir la historia completa de la cuenta.
    /// </summary>
    Task<IReadOnlyList<PresupuestoMovimientoDto>> ListarMovimientosAsync(
        string idPresupuesto, string conCuentaCode, CancellationToken ct = default);

    /// <summary>
    /// Datos de impresión del reporte de ejecución (mismos filtros que la pantalla). Sirve tanto
    /// para el PDF como para el Excel: el controlador exporta el mismo reporte en los dos formatos.
    /// </summary>
    Task<PresupuestoEjecucionImpresionDto> GetDatosImpresionEjecucionAsync(
        PresupuestoEjecucionFilterDto? filtro, string? impresoPor, CancellationToken ct = default);

    /// <summary>Datos de impresión del reporte de compromisos pendientes.</summary>
    Task<PresupuestoCompromisosImpresionDto> GetDatosImpresionCompromisosAsync(
        PresupuestoCompromisoFilterDto? filtro, string? impresoPor, CancellationToken ct = default);

    /// <summary>Modo del control por módulo. Siempre devuelve los cuatro módulos conocidos.</summary>
    Task<IReadOnlyList<PresupuestoControlConfigDto>> ListarConfiguracionAsync(CancellationToken ct = default);

    /// <summary>
    /// Guarda el modo de un módulo. Es la operación que enciende o apaga el control sin desplegar
    /// nada, así que exige permiso de edición desde el controlador.
    /// </summary>
    Task GuardarConfiguracionAsync(PresupuestoControlConfigDto dto, string usuario, CancellationToken ct = default);
}
