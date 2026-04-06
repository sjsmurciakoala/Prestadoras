using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.FacturacionMiscelaneos;
using SIAD.Services.FacturacionMiscelaneos;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/facturacion/miscelaneos")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.FacturacionMiscelaneos)]
public class FacturacionMiscelaneosController : ControllerBase
{
    private readonly IFacturacionMiscelaneosService _service;

    public FacturacionMiscelaneosController(IFacturacionMiscelaneosService service)
    {
        _service = service;
    }

    [HttpGet("clientes")]
    public async Task<IActionResult> BuscarClientes([FromQuery] string? query, CancellationToken ct)
    {
        var clientes = await _service.BuscarClientesAsync(query, ct);
        return Ok(clientes);
    }

    [HttpGet("clientes/{clave}")]
    public async Task<IActionResult> ObtenerCliente(string clave, CancellationToken ct)
    {
        var cliente = await _service.ObtenerClienteAsync(clave, ct);
        return cliente is null ? NotFound() : Ok(cliente);
    }

    [HttpGet("categorias")]
    public async Task<IActionResult> ObtenerCatalogo(CancellationToken ct)
    {
        var catalogo = await _service.ListarCatalogoAsync(ct);
        return Ok(catalogo);
    }

    [HttpPost("recibos")]
    public async Task<IActionResult> CrearRecibo([FromBody] FacturaMiscelaneoCrearDto dto, CancellationToken ct)
    {
        ResponseModelDto resultado = await _service.CrearReciboAsync(dto, ct);

        if (!resultado.Success)
        {
            return BadRequest(resultado);
        }

        return Ok(resultado);
    }

    [HttpGet("recibos/{numero:int}")]
    public async Task<IActionResult> ObtenerRecibo(int numero, CancellationToken ct)
    {
        var recibo = await _service.ObtenerReciboAsync(numero, ct);
        return recibo is null ? NotFound() : Ok(recibo);
    }

    // ── CRUD catálogo misceláneos ──

    [HttpGet("catalogo/{id:int}")]
    public async Task<IActionResult> ObtenerCatalogoItem(int id, CancellationToken ct)
    {
        var item = await _service.ObtenerCatalogoItemAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost("catalogo")]
    public async Task<IActionResult> CrearCatalogoItem([FromBody] MiscelaneoCatalogoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var creado = await _service.CrearCatalogoItemAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(ObtenerCatalogoItem), new { id = creado.Id }, creado);
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

    [HttpPut("catalogo/{id:int}")]
    public async Task<IActionResult> ActualizarCatalogoItem(int id, [FromBody] MiscelaneoCatalogoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var actualizado = await _service.ActualizarCatalogoItemAsync(id, dto, usuario, ct);
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

    [HttpDelete("catalogo/{id:int}")]
    public async Task<IActionResult> EliminarCatalogoItem(int id, CancellationToken ct)
    {
        var ok = await _service.EliminarCatalogoItemAsync(id, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }
}

