using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Letras;
using SIAD.Services.Letras;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
public class LetrasController : ControllerBase
{
    private readonly ILetrasService _service;

    public LetrasController(ILetrasService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] LetraFilterDto? filtro, CancellationToken ct)
    {
        var letras = await _service.GetLetrasAsync(filtro ?? new LetraFilterDto(), ct);
        return Ok(letras);
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] LetraFilterDto? filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        if (take <= 0)
            take = 50;

        if (take > 500)
            take = 500;

        if (skip < 0)
            skip = 0;

        var letras = await _service.GetLetrasPagedAsync(
            filtro ?? new LetraFilterDto(),
            skip,
            take,
            sortField,
            sortDesc,
            ct);
        
        return Ok(letras);
    }

    [HttpGet("{letra}")]
    public async Task<IActionResult> GetById(string letra, CancellationToken ct)
    {
        var data = await _service.GetLetraAsync(letra, ct);
        return data is null ? NotFound() : Ok(data);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] LetraEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            await _service.CreateLetraAsync(dto, usuario, ct);
            var creado = await _service.GetLetraAsync(dto.Letra, ct);
            return CreatedAtAction(nameof(GetById), new { letra = dto.Letra }, creado);
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

    [HttpPut("{letra}")]
    public async Task<IActionResult> Put(string letra, [FromBody] LetraEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            await _service.UpdateLetraAsync(letra, dto, usuario, ct);
            var actualizado = await _service.GetLetraAsync(letra, ct);
            return Ok(actualizado);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{letra}")]
    public async Task<IActionResult> Delete(string letra, CancellationToken ct)
    {
        try
        {
            var result = await _service.DeleteLetraAsync(letra, ct);
            return result ? Ok() : NotFound();
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

