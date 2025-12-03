using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Medidores;
using SIAD.Services.Medidores;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize] // ajusta si deseas permitir anónimos
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

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(MedidorDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var medidor = await _medidores.GetAsync(id, ct);
        return medidor is null ? NotFound() : Ok(medidor);
    }

    [HttpGet("{id:int}/historial")]
    [ProducesResponseType(typeof(IReadOnlyList<MedidorHistorialDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistorial(int id, [FromQuery] int take = 12, CancellationToken ct = default)
        => Ok(await _medidores.GetHistorialAsync(id, take, ct));

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
