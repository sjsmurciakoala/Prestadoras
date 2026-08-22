using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Services.Mantenimientos;
using apc.Security;

namespace apc.Controllers.Mantenimientos;

/// <summary>
/// Mantenimiento del catálogo de formatos fiscales: máscara y validación del No. de factura
/// (SAR) y del CAI que se transcriben del proveedor.
/// </summary>
[ApiController]
[Route("api/mantenimientos/formatos-fiscales")]
[ModuleAuthorize(PermissionModules.Configuracion, PermissionResources.Configuracion.FormatosFiscales)]
public sealed class FormatosFiscalesController : ControllerBase
{
    private readonly IFormatoFiscalService _service;

    public FormatosFiscalesController(IFormatoFiscalService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] FormatoFiscalFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] FormatoFiscalEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.CreateAsync(dto, User?.Identity?.Name ?? "system", ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] FormatoFiscalEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var ok = await _service.DeactivateAsync(id, User?.Identity?.Name ?? "system", ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

/// <summary>
/// Entrega los formatos activos a cualquier usuario autenticado, con la máscara de DevExpress,
/// el patrón y el ejemplo ya derivados.
/// </summary>
/// <remarks>
/// Controlador aparte y sin permiso de módulo a propósito, igual que
/// <see cref="Contabilidad.AccountFormatController"/>: quien captura una recepción de compra puede
/// no tener acceso al módulo Configuración, y sin este GET no podría ni teclear el No. de factura.
/// Solo expone formato, nunca datos de negocio.
/// </remarks>
[ApiController]
[Route("api/mantenimientos/formatos-fiscales/lookup")]
[Authorize]
public sealed class FormatosFiscalesLookupController : ControllerBase
{
    private readonly IFormatoFiscalService _service;

    public FormatosFiscalesLookupController(IFormatoFiscalService service) => _service = service;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<FormatoFiscalLookupDto>>> Obtener(CancellationToken ct)
        => Ok(await _service.GetLookupAsync(ct));
}
