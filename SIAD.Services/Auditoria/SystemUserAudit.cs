namespace SIAD.Services.Auditoria;

// Fallback de ICurrentUserAudit cuando no hay HttpContext (WS/procesos de fondo).
// En el portal apc se reemplaza por la impl sobre IHttpContextAccessor.
public sealed class SystemUserAudit : ICurrentUserAudit
{
    public string Usuario => "system";
}
