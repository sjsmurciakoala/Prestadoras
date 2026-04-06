using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Catalogos;
using SIAD.Services.Catalogos;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/catalogos")]
[ModuleAuthorize(PermissionModules.Inventario)]
public class CatalogosController : ControllerBase
{
    private readonly ICatalogosService _catalogosService;

    public CatalogosController(ICatalogosService catalogosService)
    {
        _catalogosService = catalogosService;
    }

    [HttpGet("abogados")]
    public async Task<IActionResult> GetAbogados(CancellationToken ct)
        => Ok(await _catalogosService.GetAbogadosAsync(ct));

    [HttpGet("barrios")]
    public async Task<IActionResult> GetBarrios(CancellationToken ct)
        => Ok(await _catalogosService.GetBarriosAsync(ct));

    [HttpGet("servicios")]
    public async Task<IActionResult> GetServicios(CancellationToken ct)
        => Ok(await _catalogosService.GetServiciosAsync(ct));

    [HttpGet("tipos-uso")]
    public async Task<IActionResult> GetTiposUso(CancellationToken ct)
        => Ok(await _catalogosService.GetTiposUsoAsync(ct));

    [HttpGet("letras")]
    public async Task<IActionResult> GetLetras(CancellationToken ct)
        => Ok(await _catalogosService.GetLetrasAsync(ct));

    [HttpGet("letras-servicio")]
    public async Task<IActionResult> GetLetrasServicio([FromQuery] string? tipoUsoCodigo, [FromQuery] int? categoriaId, CancellationToken ct)
    {
        if (!int.TryParse(tipoUsoCodigo, out var tipo) || categoriaId is null)
        {
            return Ok(Array.Empty<LetraServicioLookupDto>());
        }

        return Ok(await _catalogosService.GetLetrasTarifaAsync(tipo, categoriaId.Value, ct));
    }

    [HttpGet("categorias-por-tipo")]
    public async Task<IActionResult> GetCategoriasPorTipo([FromQuery] int? tipoUsoCodigo, CancellationToken ct)
    {
        if (tipoUsoCodigo is null)
        {
            return Ok(Array.Empty<int>());
        }

        return Ok(await _catalogosService.GetCategoriasPorTipoAsync(tipoUsoCodigo.Value, ct));
    }
}

