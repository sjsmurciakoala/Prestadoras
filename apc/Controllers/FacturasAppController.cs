using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Services.AppLectores;
using apc.Security;

namespace apc.Controllers;

/// <summary>
/// Consulta de facturas emitidas vía sincronización de la app de lectores V3
/// (filas de <c>adm_cai_correlativo_emitido</c> con <c>lectura_uuid</c>).
/// Solo lectura; vive junto al mantenimiento de lectores (módulo configuración).
/// </summary>
[ApiController]
[Route("api/facturas-app")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class FacturasAppController : ControllerBase
{
    private readonly IFacturasAppService _service;

    public FacturasAppController(IFacturasAppService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FacturaAppFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));
}
