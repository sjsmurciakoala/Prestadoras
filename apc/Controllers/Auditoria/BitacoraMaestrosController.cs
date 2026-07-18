using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Services.Auditoria;
using apc.Security;

namespace apc.Controllers.Auditoria;

[ApiController]
[Route("api/auditoria/bitacora-maestros")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class BitacoraMaestrosController : ControllerBase
{
    private readonly IBitacoraMaestrosService _service;
    public BitacoraMaestrosController(IBitacoraMaestrosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Buscar([FromQuery] BitacoraMaestroFilterDto filtro, CancellationToken ct)
        => Ok(await _service.BuscarAsync(filtro, ct));
}
