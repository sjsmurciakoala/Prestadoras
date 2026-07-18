namespace SIAD.Services.Auditoria;

// Provee el usuario actual al interceptor/writer (impl en apc sobre IHttpContextAccessor).
public interface ICurrentUserAudit
{
    string Usuario { get; }
}
