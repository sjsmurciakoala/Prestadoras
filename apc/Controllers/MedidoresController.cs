using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Medidores;
using SIAD.Services.Medidores;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)] // ajusta si deseas permitir anónimos
public class MedidoresController : ControllerBase
{
    private readonly IMedidoresService _medidores;

    public MedidoresController(IMedidoresService medidores)
    {
        _medidores = medidores;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MedidorListDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] MedidorFilterDto filtro, CancellationToken ct)
        => Ok(await _medidores.SearchAsync(filtro, ct));

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] MedidorFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        var result = await _medidores.GetPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MedidorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var medidor = await _medidores.GetAsync(id, ct);
        return medidor is null ? NotFound() : Ok(medidor);
    }

    [HttpGet("{id:int}/edit")]
    [ProducesResponseType(typeof(MedidorEditDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetEdit(int id, CancellationToken ct)
    {
        var medidor = await _medidores.GetEditByIdAsync(id, ct);
        return medidor is null ? NotFound() : Ok(medidor);
    }

    [HttpGet("{id:int}/historial")]
    [ProducesResponseType(typeof(IReadOnlyList<MedidorHistorialDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistorial(int id, [FromQuery] int take = 12, CancellationToken ct = default)
        => Ok(await _medidores.GetHistorialAsync(id, take, ct));

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] MedidorEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var creado = await _medidores.CreateAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(GetEdit), new { id = creado.Id }, creado);
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

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] MedidorEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var actualizado = await _medidores.UpdateAsync(id, dto, usuario, ct);
            return Ok(actualizado);
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

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var usuario = User?.Identity?.Name ?? "system";
        var ok = await _medidores.DeactivateAsync(id, usuario, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    public record AssignRequest(int MedidorId, int ClienteId);

    [HttpPost("asignar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Asignar([FromBody] AssignRequest request, CancellationToken ct)
    {
        if (request is null)
            return BadRequest("Datos incompletos.");

        var success = await _medidores.AssignToClienteAsync(request.MedidorId, request.ClienteId, ct);
        return success ? NoContent() : BadRequest("No se pudo asignar el medidor.");
    }

    public record LecturaSinMedidorRequest(string Clave, DateTime Fecha, decimal Lectura, string Usuario);

    [HttpPost("lecturas-sin-medidor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegistrarLecturaSinMedidor([FromBody] LecturaSinMedidorRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Clave))
            return BadRequest("Datos incompletos.");

        await _medidores.RegistrarLecturaSinMedidorAsync(request.Clave, request.Fecha, request.Lectura, request.Usuario, ct);
        return NoContent();
    }
}

