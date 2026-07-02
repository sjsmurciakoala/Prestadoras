namespace SIAD.Core.DTOs.Cobranza;

// ── Catálogos ──────────────────────────────────────────────────────────────

public record AccionCobranzaCatalogoDto(int CodAccion, string Nombre);

public record ObservacionCobranzaCatalogoDto(int Id, string Observacion);

public record AbogadoCobranzaLookupDto(int AbogadoId, string Codigo, string Nombre);

// ── Registro de una acción ─────────────────────────────────────────────────

public record RegistrarAccionCobranzaRequest(
    string ClienteClave,
    int CodAccion,
    int? CodObservacion,
    int? AbogadoId,
    string? Observacion,
    string? EjecutadoPor);   // persona que ejecutó la acción (tercero, ingreso manual)

// ── DTO de lectura (historial) ─────────────────────────────────────────────

public record AccionCobranzaDto(
    int Id,
    DateTime Fecha,
    int? CodAccion,
    string Accion,
    int? CodObservacion,
    string? NombreObservacion,  // texto de axl_observacion_cobranza.observacion
    string? Observacion,        // observación libre adicional
    int? AbogadoId,
    string? EjecutadoPor,
    int? DocumentoId = null);   // id del snapshot, si la acción generó documento

// ── Bloqueo / Desbloqueo ──────────────────────────────────────────────────

public record BloquearClienteRequest(
    string ClienteClave,
    bool Bloquear,
    string? Motivo,
    string Password);   // contraseña del usuario de sesión para autorizar

public record BloqueoClienteEstadoDto(
    string ClienteClave,
    string NombreCliente,
    bool Bloqueado);

// ── CRUD Catálogos (mantenimiento) ─────────────────────────────────────────

public record AccionCobranzaCrudDto(
    int CodAccion, string Nombre, bool Activo,
    bool GeneraDocumento = false, string? DocumentoCodigo = null);

public record AccionCobranzaSaveDto(
    int? CodAccion, string Nombre, bool Activo,
    bool GeneraDocumento = false, string? DocumentoCodigo = null);

// Resultado del registro de una acción (incluye el documento generado, si aplica)
public record RegistrarAccionResultadoDto(
    int AccionId,
    int? DocumentoId,
    bool DocumentoGenerado,
    string? DocumentoError);

public record ObservacionCobranzaCrudDto(int Id, string Observacion, bool Activo);

public record ObservacionCobranzaSaveDto(int? Id, string Observacion, bool Activo);

// ── Historial global (todos los clientes) ──────────────────────────────────

public record AccionCobranzaHistorialDto(
    int Id,
    DateTime Fecha,
    string ClienteClave,
    string? ClienteNombre,
    string Accion,
    string? NombreObservacion,  // texto de axl_observacion_cobranza (resultado)
    string? Observacion,        // observación libre adicional
    string? Abogado,
    string? EjecutadoPor);

public static class CobranzaHistorialConstantes
{
    // Tope de seguridad de la consulta global; la UI avisa cuando se alcanza.
    public const int MaxFilas = 5000;
}
