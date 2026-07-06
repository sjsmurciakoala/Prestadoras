using apc.MobileApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.MobileApi;
using SIAD.Services.MobileApi;

namespace apc.MobileApi.Controllers;

/// <summary>
/// Catálogo de condiciones de lectura de la empresa (spec §4). Cierra el TODO(l8)
/// que dejó L7: la app descarga las condiciones con su tipo para decidir si exige
/// lectura y cómo factura el motor V3. La empresa sale de la sesión (A6), nunca de
/// un parámetro del cliente.
/// </summary>
[ApiController]
[Route("api/condiciones")]
public sealed class CondicionesController : ControllerBase
{
    private readonly ILectoresMobileService _service;
    private readonly MobileApiRequestContext _requestContext;

    public CondicionesController(ILectoresMobileService service, MobileApiRequestContext requestContext)
    {
        _service = service;
        _requestContext = requestContext;
    }

    /// <summary>Condiciones ACTIVAS de la empresa, ordenadas; requiereLectura derivado del tipo.</summary>
    [HttpGet]
    public async Task<ActionResult<List<CondicionLecturaDto>>> Get(CancellationToken ct)
    {
        var condiciones = await _service.GetCondicionesAsync(_requestContext.CompanyId, ct);
        return Ok(condiciones);
    }
}
