using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.TalentoHumano;
using SIAD.Services.TalentoHumano;
using apc.Security;

namespace apc.Controllers.TalentoHumano;

[ApiController]
[Route("api/talentohumano/empleados")]
[ModuleAuthorize(PermissionModules.TalentoHumano)]
public sealed class EmpleadosController : ControllerBase
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly IEmpleadosService _service;
    public EmpleadosController(IEmpleadosService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] EmpleadoFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("lookup")]
    public async Task<IActionResult> GetLookup(CancellationToken ct) => Ok(await _service.GetLookupAsync(ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] EmpleadoEditDto dto, CancellationToken ct)
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
    public async Task<IActionResult> Update(int id, [FromBody] EmpleadoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, User?.Identity?.Name ?? "system", ct));
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
        var ok = await _service.DeactivateAsync(id, User?.Identity?.Name ?? "system", ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    [HttpGet("plantilla-excel")]
    public IActionResult DescargarPlantilla()
    {
        var bytes = _service.GenerarPlantillaExcel();
        return File(bytes, ExcelContentType, "plantilla-empleados.xlsx");
    }

    [HttpPost("importar-excel")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> ImportarExcel([FromForm(Name = "archivo")] IFormFile? archivo, CancellationToken ct)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return BadRequest(new ProblemDetails { Title = "Archivo requerido", Detail = "Debe proporcionar un archivo Excel (.xlsx)." });
        }

        var extension = Path.GetExtension(archivo.FileName)?.ToLowerInvariant();
        if (extension != ".xlsx")
        {
            return BadRequest(new ProblemDetails { Title = "Tipo de archivo no válido", Detail = "Solo se acepta un archivo .xlsx." });
        }

        try
        {
            await using var stream = archivo.OpenReadStream();
            var resultado = await _service.ImportarExcelAsync(stream, User?.Identity?.Name ?? "system", ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
