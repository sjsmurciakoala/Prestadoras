using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.AuxiliarLectura;
using SIAD.Services.AuxiliarLectura;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)] // o AllowAnonymous si prefieres
public class AuxiliarLecturaController : ControllerBase
{
    private readonly IAuxiliarLecturaService _service;

    public AuxiliarLecturaController(IAuxiliarLecturaService service) => _service = service;

    [HttpGet("periodo-actual")]
    public async Task<IActionResult> GetPeriodoActual(CancellationToken ct)
        => Ok(await _service.GetPeriodoActualAsync(ct));

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] AuxiliarLecturaFilterDto filtro, CancellationToken ct)
        => Ok(await _service.SearchAsync(filtro, ct));

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged([FromQuery] AuxiliarLecturaFilterDto filtro, CancellationToken ct)
        => Ok(await _service.SearchPagedAsync(filtro, ct));

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] GenerarPeriodoRequest request, CancellationToken ct)
    {
        try
        {
            var ok = await _service.GenerarPeriodoAsync(request.Anio, request.Mes, request.Ciclo, request.Usuario, ct);
            return ok ? NoContent() : BadRequest("No se pudo generar el período (posible duplicado).");
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    public record GenerarPeriodoRequest(int Anio, int Mes, string Ciclo, string Usuario);

    [HttpPost("cierre")]
    public async Task<IActionResult> Cerrar([FromBody] PeriodoRequest request, CancellationToken ct)
    {
        var ok = await _service.CerrarPeriodoAsync(request.Anio, request.Mes, ct);
        return ok ? NoContent() : BadRequest("Existen lecturas pendientes o el periodo no existe.");
    }

    [HttpDelete]
    public async Task<IActionResult> Eliminar([FromQuery] int anio, [FromQuery] int mes, CancellationToken ct)
    {
        var ok = await _service.EliminarPeriodoAsync(anio, mes, ct);
        return ok ? NoContent() : BadRequest("El periodo ya tiene lecturas registradas o no existe.");
    }

    [HttpPost("masivo")]
    public async Task<IActionResult> CargaMasiva([FromBody] LecturaMasivaDto payload, CancellationToken ct)
    {
        await _service.RegistrarLecturasMasivasAsync(payload, ct);
        return NoContent();
    }

    public record PeriodoRequest(int Anio, int Mes);
}

