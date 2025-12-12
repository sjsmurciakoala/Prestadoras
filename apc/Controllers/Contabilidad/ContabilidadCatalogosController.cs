using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/catalogos")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public class ContabilidadCatalogosController : ControllerBase
{
    private readonly IContabilidadCatalogosService _catalogosService;

    public ContabilidadCatalogosController(IContabilidadCatalogosService catalogosService)
    {
        _catalogosService = catalogosService;
    }

    [HttpGet("plan-cuentas")]
    public async Task<IActionResult> GetPlanCuentas(CancellationToken cancellationToken)
    {
        var cuentas = await _catalogosService.GetPlanCuentasAsync(cancellationToken);
        return Ok(cuentas);
    }

    [HttpPost("plan-cuentas")]
    public async Task<IActionResult> SavePlanCuenta([FromBody] PlanCuentaUpsertDto request, CancellationToken cancellationToken)
    {
        var userName = User?.Identity?.Name ?? "system";
        var resultId = await _catalogosService.SavePlanCuentaAsync(request with { User = userName }, cancellationToken);
        return Ok(new { id = resultId });
    }

    [HttpGet("centros-costo")]
    public async Task<IActionResult> GetCentrosCosto(CancellationToken cancellationToken)
    {
        var centros = await _catalogosService.GetCentrosCostoAsync(cancellationToken);
        return Ok(centros);
    }

    [HttpPost("centros-costo")]
    public async Task<IActionResult> SaveCentroCosto([FromBody] CentroCostoUpsertDto request, CancellationToken cancellationToken)
    {
        var userName = User?.Identity?.Name ?? "system";
        var resultId = await _catalogosService.SaveCentroCostoAsync(request with { User = userName }, cancellationToken);
        return Ok(new { id = resultId });
    }

    [HttpGet("diarios")]
    public async Task<IActionResult> GetDiarios(CancellationToken cancellationToken)
    {
        var diarios = await _catalogosService.GetDiariosAsync(cancellationToken);
        return Ok(diarios);
    }

    [HttpPost("diarios")]
    public async Task<IActionResult> SaveDiario([FromBody] DiarioUpsertDto request, CancellationToken cancellationToken)
    {
        var userName = User?.Identity?.Name ?? "system";
        var resultId = await _catalogosService.SaveDiarioAsync(request with { User = userName }, cancellationToken);
        return Ok(new { id = resultId });
    }

    [HttpGet("periodos")]
    public async Task<IActionResult> GetPeriodos(CancellationToken cancellationToken)
    {
        var periodos = await _catalogosService.GetPeriodosAsync(cancellationToken);
        return Ok(periodos);
    }

    [HttpPost("periodos")]
    public async Task<IActionResult> SavePeriodo([FromBody] PeriodoContableUpsertDto request, CancellationToken cancellationToken)
    {
        var userName = User?.Identity?.Name ?? "system";
        var resultId = await _catalogosService.SavePeriodoAsync(request with { User = userName }, cancellationToken);
        return Ok(new { id = resultId });
    }

    [HttpPost("periodos/{periodId:long}/cerrar")]
    public async Task<IActionResult> ClosePeriodo(long periodId, CancellationToken cancellationToken)
    {
        var userName = User?.Identity?.Name ?? "system";
        var closed = await _catalogosService.ClosePeriodoAsync(periodId, userName, cancellationToken);
        return closed ? Ok() : NotFound();
    }
}
