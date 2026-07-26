using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Caja;
using SIAD.Services.Caja;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Caja)]
public class CajaController : ControllerBase
{
    private readonly ICajaService _cajaService;

    public CajaController(ICajaService cajaService)
    {
        _cajaService = cajaService;
    }

    // GET api/caja/sesion-activa?usuario=xxx
    [HttpGet("sesion-activa")]
    public async Task<IActionResult> GetSesionActiva([FromQuery] string usuario)
        => Ok(await _cajaService.ObtenerSesionActivaAsync(usuario));

    // POST api/caja/abrir
    [HttpPost("abrir")]
    public async Task<IActionResult> Abrir([FromBody] AbrirCajaRequestDto request)
    {
        var result = await _cajaService.AbrirCajaAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // POST api/caja/cerrar
    [HttpPost("cerrar")]
    public async Task<IActionResult> Cerrar([FromBody] CerrarCajaRequestDto request)
    {
        var result = await _cajaService.CerrarCajaAsync(request);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    // GET api/caja/sesion/{sesionId}/resumen
    [HttpGet("sesion/{sesionId:int}/resumen")]
    public async Task<IActionResult> GetResumen(int sesionId)
    {
        var resumen = await _cajaService.ObtenerResumenAsync(sesionId);
        return resumen is null ? NotFound() : Ok(resumen);
    }

    // GET api/caja/historial?usuario=xxx
    [HttpGet("historial")]
    public async Task<IActionResult> GetHistorial([FromQuery] string usuario)
        => Ok(await _cajaService.ListarHistorialAsync(usuario));

    // GET api/caja/cajas — cajas físicas de la empresa con su disponibilidad
    // (varias cajas simultáneas; la apertura elige una — unificación F2)
    [HttpGet("cajas")]
    public async Task<IActionResult> GetCajas()
        => Ok(await _cajaService.ListarCajasAsync());
}
