using SIAD.Core.DTOs.Proveedores;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Bitácora de incidencias de recepción (<c>prv_recepcion_incidencia</c>, F4).
/// <para>
/// Alimenta el criterio CALIDAD del scorecard: <c>fn_prv_evaluacion_metricas</c> cuenta las
/// recepciones sin incidencia. Mientras la empresa no registre ninguna, ese criterio se reporta
/// sin datos a propósito, para no regalarle el 100% a todos los proveedores.
/// </para>
/// </summary>
public interface IRecepcionIncidenciaService
{
    Task<IReadOnlyList<RecepcionIncidenciaDto>> GetAsync(
        RecepcionIncidenciaFilterDto? filtro = null, CancellationToken ct = default);

    Task<RecepcionIncidenciaDto?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<RecepcionIncidenciaDto> CrearAsync(
        RecepcionIncidenciaUpsertDto dto, string usuario, CancellationToken ct = default);

    Task<RecepcionIncidenciaDto> ActualizarAsync(
        int id, RecepcionIncidenciaUpsertDto dto, string usuario, CancellationToken ct = default);

    /// <summary>Borra la incidencia. Devuelve false si no existe en la empresa actual.</summary>
    Task<bool> EliminarAsync(int id, CancellationToken ct = default);

    /// <summary>Recepciones NO anuladas del proveedor, para elegir a cuál se le registra la incidencia.</summary>
    Task<IReadOnlyList<RecepcionIncidenciaLookupDto>> BuscarRecepcionesAsync(
        string codProveedor, string? search = null, CancellationToken ct = default);
}
