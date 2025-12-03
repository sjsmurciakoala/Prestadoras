using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.FacturacionMiscelaneos;
using SIAD.Services.FacturacionMiscelaneos;

namespace apc.Controllers;

[ApiController]
[Route("api/facturacion/miscelaneos")]
[Authorize]
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
}
