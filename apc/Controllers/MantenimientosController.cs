using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Services.Mantenimientos;
using SIAD.Core.Constants;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/mantenimientos")]
[Authorize]
public class MantenimientosController : ControllerBase
{
    private readonly IMantenimientosService _service;

    public MantenimientosController(IMantenimientosService service)
    {
        _service = service;
    }

    [HttpGet("recargo-mora")]
    public async Task<IActionResult> ObtenerRecargoMora(CancellationToken ct)
        => Ok(await _service.ObtenerRecargoMoraAsync(ct));

    // Escribir configuracion exige permiso; los GET quedan abiertos a cualquier autenticado
    // porque Clientes y Cobranza los consumen para poblar sus formularios.
    [ModuleAuthorize(PermissionModules.Configuracion, PermissionAction.Edit)]
    [HttpPost("recargo-mora")]
    public async Task<IActionResult> GuardarRecargoMora([FromBody] RecargoMoraDto dto, CancellationToken ct)
    {
        var resp = await _service.GuardarRecargoMoraAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }

    [HttpGet("ajustes-tarifarios")]
    public async Task<IActionResult> ListarAjustesTarifarios(CancellationToken ct)
        => Ok(await _service.ListarAjustesTarifariosAsync(ct));

    [ModuleAuthorize(PermissionModules.Configuracion, PermissionAction.Edit)]
    [HttpPost("ajustes-tarifarios")]
    public async Task<IActionResult> GuardarAjusteTarifario([FromBody] AjusteTarifarioSaveRequestDto dto, CancellationToken ct)
    {
        var resp = await _service.GuardarAjusteTarifarioAsync(dto, ct);
        return resp.Success ? Ok(resp) : BadRequest(resp);
    }
}
