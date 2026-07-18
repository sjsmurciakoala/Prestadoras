using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Services.Auditoria;

namespace apc.Controllers.Auditoria;

[ApiController]
[Route("api/auditoria/configuracion")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class AuditoriaConfigController : ControllerBase
{
    private readonly IAuditoriaConfigService _service;
    public AuditoriaConfigController(IAuditoriaConfigService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct) => Ok(await _service.GetAsync(ct));

    [HttpPut]
    public async Task<IActionResult> Guardar([FromBody] List<AuditoriaConfigItemDto> items, CancellationToken ct)
    {
        await _service.GuardarAsync(items, User?.Identity?.Name ?? "system", ct);
        return Ok(new { success = true });
    }
}
