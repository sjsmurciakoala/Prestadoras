using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.TiposDocumentoFiscal;
using SIAD.Services.TiposDocumentoFiscal;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/tipos-documento-fiscal")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public class TiposDocumentoFiscalController : ControllerBase
{
    private readonly ITiposDocumentoFiscalService _service;

    public TiposDocumentoFiscalController(ITiposDocumentoFiscalService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => Ok(await _service.ListarAsync(ct));

    [ModuleAuthorize(PermissionModules.Configuracion, action: PermissionAction.Edit)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(short id, [FromBody] TipoDocumentoFiscalUpdateDto dto, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var resp = await _service.ActualizarAsync(id, dto, usuario, ct);
        return Ok(resp);
    }
}
