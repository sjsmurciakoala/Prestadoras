using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Ordenes;
using SIAD.Services.Ordenes;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Inventario)]
public class OrdenesController : ControllerBase
{
    private readonly IOrdenesService _ordenesService;

    public OrdenesController(IOrdenesService ordenesService)
    {
        _ordenesService = ordenesService;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] OrdenTrabajoFilterDto filtro, CancellationToken cancellationToken)
    {
        var ordenes = await _ordenesService.GetOrdenesAsync(filtro, cancellationToken);
        return Ok(ordenes);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)
    {
        var orden = await _ordenesService.GetOrdenAsync(id, cancellationToken);
        return orden is null ? NotFound() : Ok(orden);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] CrearOrdenTrabajoDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _ordenesService.CrearOrdenAsync(dto, cancellationToken);
        return resultado.Exitoso ? Ok(resultado) : BadRequest(resultado);
    }

    [HttpPost("asignaciones")]
    public async Task<IActionResult> Asignar([FromBody] OrdenTrabajoAsignacionDto dto, CancellationToken cancellationToken)
    {
        var resultado = await _ordenesService.AsignarOrdenesAsync(dto, cancellationToken);
        return resultado.Exitoso ? Ok(resultado) : BadRequest(resultado);
    }

    [HttpGet("usuarios")]
    public async Task<IActionResult> GetUsuarios([FromQuery] int? tipo, CancellationToken cancellationToken)
    {
        var usuarios = await _ordenesService.GetUsuariosMiOrdenAsync(tipo, cancellationToken);
        return Ok(usuarios);
    }

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos([FromQuery] string departamento, [FromQuery] string? q, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(departamento))
        {
            return BadRequest("El parámetro 'departamento' es obligatorio.");
        }

        var tipos = await _ordenesService.BuscarTiposOrdenAsync(departamento, q, take, cancellationToken);
        return Ok(tipos);
    }

    [HttpGet("propietarios")]
    public async Task<IActionResult> GetPropietarios([FromQuery] string? q, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var propietarios = await _ordenesService.BuscarPropietariosAsync(q, take, cancellationToken);
        return Ok(propietarios);
    }

    [HttpGet("estados")]
    public async Task<IActionResult> GetEstados([FromQuery] string? q, [FromQuery] int take = 20, CancellationToken cancellationToken = default)
    {
        var estados = await _ordenesService.BuscarEstadosOrdenAsync(q, take, cancellationToken);
        return Ok(estados);
    }

    /// <summary>
    /// Devuelve la imagen (o archivo) de un adjunto. Va por un endpoint propio para no
    /// cargar los bytes de todas las fotos dentro del JSON del detalle de la orden.
    /// </summary>
    [HttpGet("adjuntos/{adjuntoId:int}/contenido")]
    public async Task<IActionResult> GetAdjuntoContenido(int adjuntoId, CancellationToken cancellationToken)
    {
        var adjunto = await _ordenesService.GetAdjuntoContenidoAsync(adjuntoId, cancellationToken);

        if (adjunto is null)
        {
            return NotFound();
        }

        // Los adjuntos son inmutables: una vez subidos desde la app no cambian, asi que
        // se pueden cachear en el navegador y evitar re-descargarlos al abrir la orden.
        Response.Headers.CacheControl = "private, max-age=86400";

        return File(adjunto.Contenido, adjunto.ContentType, adjunto.NombreArchivo);
    }

    [HttpGet("coordenadas")]
    public async Task<IActionResult> GetCoordenadas(CancellationToken cancellationToken)
    {
        var coordenadas = await _ordenesService.GetCoordenadasAsync(cancellationToken);
        return Ok(coordenadas);
    }
}

