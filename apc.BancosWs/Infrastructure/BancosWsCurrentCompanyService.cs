using SIAD.Core.Tenancy;

namespace apc.BancosWs.Infrastructure;

/// <summary>
/// ICurrentCompanyService del host bancario: el tenant sale de la credencial
/// autenticada del request (ban_ws_credencial), no de claims. Sin credencial
/// devuelve 0 (los filtros globales de SiadDbContext no matchean nada).
/// </summary>
public sealed class BancosWsCurrentCompanyService : ICurrentCompanyService
{
    private readonly BancosWsRequestContext _context;

    public BancosWsCurrentCompanyService(BancosWsRequestContext context)
    {
        _context = context;
    }

    public long GetCompanyId() => _context.Autenticado ? _context.CompanyId : 0;
}
