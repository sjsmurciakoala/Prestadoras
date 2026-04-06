using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Informes;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using apc.Security;

namespace apc.Controllers.Informes;

[ApiController]
[Route("api/informes")]
[ModuleAuthorize(PermissionModules.Reporteria)]
public sealed class InformesController : ControllerBase
{
    private readonly IInformesCatalogoService _catalogoService;
    private readonly IInformesConsultaService _consultaService;
    private readonly ICurrentCompanyService _currentCompany;

    public InformesController(
        IInformesCatalogoService catalogoService,
        IInformesConsultaService consultaService,
        ICurrentCompanyService currentCompany)
    {
        _catalogoService = catalogoService;
        _consultaService = consultaService;
        _currentCompany = currentCompany;
    }

    [HttpGet("catalogo")]
    public async Task<IActionResult> GetCatalogo(CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var items = await _catalogoService.ListarAsync(companyId, ct);
        return Ok(items);
    }

    [HttpGet("consultas/partidas-contabilidad")]
    public async Task<IActionResult> ConsultarPartidas([FromQuery] PartidasInformeFiltroDto filtro, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var resultado = await _consultaService.ConsultarPartidasAsync(companyId, filtro, ct);
        return Ok(resultado);
    }
}
