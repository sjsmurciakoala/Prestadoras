using SIAD.Core.DTOs.Libretas;

namespace SIAD.Services.Libretas;

/// <summary>
/// Catálogo global de libretas (libro del lector, sin ciclo). El combo Libreta
/// del cliente y la pantalla de mantenimiento consumen este servicio; la
/// derivación de rutas por ciclo para apertura/cierre vive en BD
/// (fn_adm_periodo_ciclo_info) a partir de los indicativos de los clientes.
/// </summary>
public interface ILibretasService
{
    /// <summary>Todas las libretas de la empresa (activas e inactivas) para el mantenimiento.</summary>
    Task<IReadOnlyList<LibretaDto>> ListarAsync(CancellationToken ct = default);

    /// <summary>Solo activas — para el combo del formulario de cliente.</summary>
    Task<IReadOnlyList<LibretaDto>> ListarActivasAsync(CancellationToken ct = default);

    Task<LibretaDto?> ObtenerAsync(long id, CancellationToken ct = default);

    Task<long> CrearAsync(LibretaUpsertDto dto, string usuario, CancellationToken ct = default);

    Task ActualizarAsync(long id, LibretaUpsertDto dto, string usuario, CancellationToken ct = default);

    Task<bool> DesactivarAsync(long id, string usuario, CancellationToken ct = default);

    /// <summary>true si el código existe y está activo (validación server-side del cliente).</summary>
    Task<bool> ExisteActivaAsync(string codigo, CancellationToken ct = default);
}
