using System.Collections.Generic;
using SIAD.Data.Auditoria;

namespace SIAD.Services.Auditoria;

// Escribe una fila de bitácora explícitamente (para maestros que NO pasan por el
// interceptor de SaveChanges, p. ej. prv_proveedores con SQL crudo).
public interface IBitacoraMaestrosWriter
{
    Task RegistrarAsync(string tabla, string accion, string? registroId, string entidad, string descripcion,
                        IReadOnlyList<AuditDiff.Campo>? campos, CancellationToken ct = default);
}
