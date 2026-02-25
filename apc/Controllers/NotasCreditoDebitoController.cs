using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.NotasCreditoDebito;
using SIAD.Services.NotasCreditoDebito;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/facturacion/notas")]
[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.NotasCreditoDebito)]
public class NotasCreditoDebitoController : ControllerBase
{
    private readonly INotasCreditoDebitoService _service;

    public NotasCreditoDebitoController(INotasCreditoDebitoService service)
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

    [HttpGet("clientes/{clave}/configuracion")]
    public async Task<IActionResult> ObtenerConfiguracion(string clave, CancellationToken ct)
    {
        var configuracion = await _service.ObtenerConfiguracionClienteAsync(clave, ct);
        return configuracion is null ? NotFound() : Ok(configuracion);
    }

    [HttpGet("motivos")]
    public async Task<IActionResult> ListarMotivos(CancellationToken ct)
    {
        var motivos = await _service.ListarMotivosAsync(ct);
        return Ok(motivos);
    }

    [HttpGet("motivos/{id:int}")]
    public async Task<IActionResult> ObtenerMotivo(int id, CancellationToken ct)
    {
        var motivo = await _service.ObtenerMotivoAsync(id, ct);
        return motivo is null ? NotFound() : Ok(motivo);
    }

    [HttpPost]
    public async Task<IActionResult> RegistrarNota([FromBody] NotaCrearRequestDto dto, CancellationToken ct)
    {
        var respuesta = await _service.RegistrarNotaAsync(dto, ct);
        if (!respuesta.Success)
        {
            return BadRequest(respuesta);
        }

        return Ok(respuesta);
    }
}

