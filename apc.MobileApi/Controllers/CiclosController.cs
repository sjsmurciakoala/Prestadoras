using Microsoft.AspNetCore.Mvc;
using SIAD.Services.MobileApi;

namespace apc.MobileApi.Controllers;

/// <summary>Ciclo pendiente de una ruta (paridad GetCiclo del WS).</summary>
[ApiController]
[Route("api/ciclos")]
public sealed class CiclosController : ControllerBase
{
    private readonly ILectoresMobileService _service;

    public CiclosController(ILectoresMobileService service)
    {
        _service = service;
    }

    /// <summary>Devuelve el ciclo/año/mes pendiente de la ruta.</summary>
    [HttpGet("{ruta}")]
    public async Task<IActionResult> GetCiclo(string ruta, CancellationToken ct)
    {
        var ciclo = await _service.GetCicloAsync(ruta, ct);
        return Ok(ciclo);
    }
}
