using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.TalentoHumano;
using SIAD.Services.TalentoHumano;

namespace apc.Controllers.TalentoHumano;

/// <summary>
/// Base compartida por los controladores de catálogos simples de Talento Humano (cargos,
/// departamentos). Cada controlador concreto solo fija su <see cref="Tipo"/> y su ruta.
/// </summary>
public abstract class CatalogoThControllerBase : ControllerBase
{
    private readonly ICatalogoThService _service;
    protected CatalogoThControllerBase(ICatalogoThService service) => _service = service;

    protected abstract CatalogoTh Tipo { get; }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CatalogoThFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(Tipo, filtro, ct));

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(CancellationToken ct) => Ok(await _service.GetLookupAsync(Tipo, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(Tipo, id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CatalogoThEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.CreateAsync(Tipo, dto, User?.Identity?.Name ?? "system", ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] CatalogoThEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.UpdateAsync(Tipo, id, dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var ok = await _service.DeactivateAsync(Tipo, id, User?.Identity?.Name ?? "system", ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}
