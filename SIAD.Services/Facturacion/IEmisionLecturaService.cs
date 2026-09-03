using SIAD.Core.DTOs.Facturacion;

namespace SIAD.Services.Facturacion;

public interface IEmisionLecturaService
{
    /// <summary>
    /// Bloque de folios del portal: lo devuelve, y si no existe o se agotó reserva otro.
    /// </summary>
    Task<BloqueCaiPortalDto> ObtenerBloqueAsync(CancellationToken ct = default);

    /// <summary>
    /// Calcula lo que se facturaria con esa lectura, sin emitir nada.
    /// </summary>
    Task<PreviewFacturaLecturaDto> PrevisualizarAsync(
        EmitirFacturaLecturaRequest request, CancellationToken ct = default);

    /// <summary>
    /// Emite la factura de una lectura capturada en el portal.
    /// Los errores de negocio vuelven en el resultado, no como excepción.
    /// </summary>
    Task<EmitirFacturaLecturaResultado> EmitirAsync(
        EmitirFacturaLecturaRequest request, string usuario, CancellationToken ct = default);
}
