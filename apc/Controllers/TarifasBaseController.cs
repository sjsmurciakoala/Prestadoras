using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.TarifasBase;
using SIAD.Services.TarifasBase;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/tarifas-base")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class TarifasBaseController : ControllerBase
{
    private readonly ITarifasBaseService _service;

    public TarifasBaseController(ITarifasBaseService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] TarifaBaseFilterDto filtro, CancellationToken ct)
    {
        var tarifas = await _service.GetAsync(filtro, ct);
        return Ok(tarifas);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] TarifaBaseFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    [HttpGet("{tipo:int}/{categoriaId:int}/{codigo}")]
    public async Task<IActionResult> GetById(int tipo, int categoriaId, string codigo, CancellationToken ct)
    {
        var tarifa = await _service.GetByIdAsync(tipo, categoriaId, codigo, ct);
        return tarifa is null ? NotFound() : Ok(tarifa);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] TarifaBaseEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var creada = await _service.CreateAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(GetById), new { tipo = creada.Tipo, categoriaId = creada.CategoriaId, codigo = creada.Codigo }, creada);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{tipo:int}/{categoriaId:int}/{codigo}")]
    public async Task<IActionResult> Update(int tipo, int categoriaId, string codigo, [FromBody] TarifaBaseEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var actualizada = await _service.UpdateAsync(tipo, categoriaId, codigo, dto, usuario, ct);
            return Ok(actualizada);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{tipo:int}/{categoriaId:int}/{codigo}")]
    public async Task<IActionResult> Delete(int tipo, int categoriaId, string codigo, CancellationToken ct)
    {
        try
        {
            var ok = await _service.DeleteAsync(tipo, categoriaId, codigo, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

