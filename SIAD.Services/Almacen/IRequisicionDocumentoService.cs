using SIAD.Core.DTOs.Almacen;

namespace SIAD.Services.Almacen;

/// <summary>
/// Documento de requisición (Fase 6): la SOLICITUD de materiales. <b>Nunca mueve inventario</b> — la
/// salida la produce el descargo (Fase 6.3). Máquina de estados:
/// Borrador → En revisión → Aprobada → (Despachada parcial/total, derivado del descargo) ·
/// En revisión → Rechazada · cualquiera sin despachar → Anulada.
/// <para>Ver <c>docs/plans/2026-08-04-requisiciones-descargos-fase6-plan.md</c>.</para>
/// </summary>
public interface IRequisicionDocumentoService
{
    Task<IReadOnlyList<RequisicionDocumentoListItemDto>> GetAsync(RequisicionDocumentoFilterDto? filtro, CancellationToken ct = default);
    Task<RequisicionDocumentoDto?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Datos para imprimir el comprobante (vale) de la requisición. Null si no existe.</summary>
    Task<RequisicionImpresionDto?> GetDatosImpresionAsync(int id, string impresoPor, CancellationToken ct = default);

    /// <summary>Crea la requisición en <c>Borrador</c> y sus renglones en la tabla plana
    /// (origen SIAD, posteado=false: nunca asienta). Idempotente por <see cref="RequisicionDocumentoDto.Uuid"/>.</summary>
    Task<RequisicionDocumentoDto> CrearAsync(RequisicionDocumentoDto dto, string user, CancellationToken ct = default);

    /// <summary>Edita una requisición en <c>Borrador</c> (cabecera + reemplazo de renglones).</summary>
    Task<RequisicionDocumentoDto> ActualizarAsync(int id, RequisicionDocumentoDto dto, string user, CancellationToken ct = default);

    /// <summary>Borrador → En revisión (queda disponible para aprobación).</summary>
    Task<RequisicionDocumentoDto> EnviarARevisionAsync(int id, string user, CancellationToken ct = default);

    /// <summary>En revisión → Aprobada. El permiso de aprobar lo resuelve el controller.</summary>
    Task<RequisicionDocumentoDto> AprobarAsync(int id, string user, CancellationToken ct = default);

    /// <summary>En revisión → Rechazada (motivo obligatorio).</summary>
    Task<RequisicionDocumentoDto> RechazarAsync(int id, string motivo, string user, CancellationToken ct = default);

    /// <summary>Anula una requisición aún sin despachar. Idempotente.</summary>
    Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default);
}
