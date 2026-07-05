using SIAD.Core.Tenancy;

namespace apc.MobileApi.Infrastructure;

/// <summary>
/// ICurrentCompanyService de la API móvil: el tenant sale de la sesión
/// autenticada del request (adm_lector_sesion → adm_lector_credencial), no de
/// claims ni de parámetros del cliente (A6). Sin sesión devuelve 0 (los filtros
/// globales de SiadDbContext no matchean nada).
/// </summary>
public sealed class MobileApiCurrentCompanyService : ICurrentCompanyService
{
    private readonly MobileApiRequestContext _context;

    public MobileApiCurrentCompanyService(MobileApiRequestContext context)
    {
        _context = context;
    }

    public long GetCompanyId() => _context.Autenticado ? _context.CompanyId : 0;
}
