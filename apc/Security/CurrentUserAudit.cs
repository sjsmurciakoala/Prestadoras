using Microsoft.AspNetCore.Http;
using SIAD.Services.Auditoria;

namespace apc.Security;

public sealed class CurrentUserAudit : ICurrentUserAudit
{
    private readonly IHttpContextAccessor _http;
    public CurrentUserAudit(IHttpContextAccessor http) => _http = http;
    public string Usuario => _http.HttpContext?.User?.Identity?.Name ?? "system";
}
