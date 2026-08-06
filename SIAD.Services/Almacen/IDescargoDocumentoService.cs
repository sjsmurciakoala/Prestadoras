using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Documento de descargo (Fase 6): la ENTREGA real de materiales. <b>Es el único del par
/// requisición/descargo que postea al kardex</b> (salida por <c>SalidaDescargo</c>). Contra una
/// requisición aprobada (entrega parcial, sube <c>cantidad_despachada</c> y deriva el estado de la
/// requisición) o directo con motivo. Se corrige por anulación con reversa, nunca por UPDATE.
/// </summary>
public interface IDescargoDocumentoService
{
    Task<IReadOnlyList<DescargoDocumentoListItemDto>> GetAsync(DescargoDocumentoFilterDto? filtro, CancellationToken ct = default);
    Task<DescargoDocumentoDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Datos para imprimir el comprobante (vale de salida) del descargo. Null si no existe.</summary>
    Task<DescargoImpresionDto?> GetDatosImpresionAsync(int id, string impresoPor, CancellationToken ct = default);

    /// <summary>
    /// Entrega (crea y postea) el descargo. Idempotente por <see cref="DescargoDocumentoDto.Uuid"/>.
    /// Contra requisición: cada renglón sirve una línea de requisición (tope
    /// <c>cantidad − cantidad_despachada</c> bajo candado). Directo: exige motivo.
    /// </summary>
    Task<DescargoDocumentoDto> EntregarAsync(DescargoDocumentoDto dto, string user, CancellationToken ct = default);

    /// <summary>
    /// Anula el descargo: reversa por renglón (la mercadería vuelve a la bodega) y, si venía de una
    /// requisición, resta <c>cantidad_despachada</c> y recomputa su estado. Idempotente.
    /// </summary>
    Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default);
}
