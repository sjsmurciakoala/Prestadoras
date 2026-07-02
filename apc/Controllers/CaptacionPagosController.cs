using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using SIAD.Core.DTOs.CaptacionPagos;

using SIAD.Services.CaptacionPagos;

using apc.Security;

using SIAD.Core.Constants;



namespace apc.Controllers;



[ApiController]

[Route("api/[controller]")]

[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CaptacionPagos)]

public class CaptacionPagosController : ControllerBase

{

    private readonly ICaptacionPagosService _service;



    public CaptacionPagosController(ICaptacionPagosService service)

    {

        _service = service;

    }



    [HttpGet("cajas")]

    public async Task<IActionResult> GetCajas(CancellationToken ct)

    {

        var cajas = await _service.ListarCatalogoCajasAsync(ct);

        return Ok(cajas);

    }



    [HttpGet("arqueos")]

    public async Task<IActionResult> GetArqueos([FromQuery] CaptacionArqueoFilterDto filtro, CancellationToken ct)

    {

        filtro ??= new CaptacionArqueoFilterDto();

        var arqueos = await _service.ListarArqueosAsync(filtro, ct);

        return Ok(arqueos);

    }



    [HttpGet("arqueos/paged")]

    public async Task<IActionResult> GetArqueosPaged(

        [FromQuery] CaptacionArqueoFilterDto? filtro,

        [FromQuery] int skip,

        [FromQuery] int take,

        [FromQuery] string? sortField,

        [FromQuery] bool sortDesc,

        CancellationToken ct)

    {

        filtro ??= new CaptacionArqueoFilterDto();

        try
        {
            var arqueos = await _service.ListarArqueosPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
            return Ok(arqueos);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // El cliente canceló la petición (p. ej. el grid se recargó al buscar). No es un error.
            return StatusCode(499);
        }

    }



    [HttpGet("miscelaneos")]

    public async Task<IActionResult> GetMiscelaneos([FromQuery] string? clienteClave, CancellationToken ct)

    {

        var recibos = await _service.ListarPagosMiscelaneosAsync(clienteClave, ct);

        return Ok(recibos);

    }



    [HttpGet("miscelaneos/paged")]

    public async Task<IActionResult> GetMiscelaneosPaged(

        [FromQuery] string? clienteClave,

        [FromQuery] int skip,

        [FromQuery] int take,

        [FromQuery] string? sortField,

        [FromQuery] bool sortDesc,

        CancellationToken ct)

    {

        var recibos = await _service.ListarPagosMiscelaneosPagedAsync(clienteClave, skip, take, sortField, sortDesc, ct);

        return Ok(recibos);

    }



    [HttpGet("{numFactura}")]

    public async Task<IActionResult> GetPago(string numFactura, CancellationToken ct)

    {

        var pago = await _service.ObtenerPagoAsync(numFactura, ct);

        return pago is null ? NotFound() : Ok(pago);

    }



    [HttpPost]

    public async Task<IActionResult> RegistrarPago([FromBody] PagoCrearDto dto, CancellationToken ct)

    {

        var respuesta = await _service.RegistrarPagoAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CaptacionPagos, PermissionAction.Edit)]

    [HttpPost("reverso")]

    public async Task<IActionResult> ReversarPago([FromBody] ReversoRequestDto dto, CancellationToken ct)

    {

        var respuesta = await _service.ReversarPagoAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    // ==================== AUTOCOMPLETADO Y BÚSQUEDA ====================



    [HttpGet("search/{term}")]

    public async Task<IActionResult> BuscarFacturas(string term, CancellationToken ct)

    {

        if (string.IsNullOrWhiteSpace(term))

        {

            return Ok(Array.Empty<BusquedaFacturaDto>());

        }



        var facturas = await _service.BuscarFacturasAsync(term, ct);

        return Ok(facturas);

    }



    [HttpGet("{numFactura}/existe")]

    public async Task<IActionResult> ExisteRegistroPago(string numFactura, CancellationToken ct)

    {

        var existe = await _service.ExisteRegistroPagoAsync(numFactura, ct);

        return Ok(new { existe });

    }



    // ==================== POSTEO MANUAL ====================



    [HttpGet("saldos-manual/{clienteClave}")]

    public async Task<IActionResult> GetSaldosPosteoManual(string clienteClave, CancellationToken ct)

    {

        var saldos = await _service.ObtenerSaldosPosteoManualAsync(clienteClave, ct);

        return Ok(saldos);

    }



    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CaptacionPagos, PermissionAction.Edit)]

    [HttpPost("posteo-manual")]

    public async Task<IActionResult> RegistrarPagoManual([FromBody] PagoManualCrearDto dto, CancellationToken ct)

    {

        var respuesta = await _service.RegistrarPagoManualAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CaptacionPagos, PermissionAction.Edit)]

    [HttpPost("posteo-manual/reverso")]

    public async Task<IActionResult> ReversarPagoManual([FromBody] ReversoManualRequestDto dto, CancellationToken ct)

    {

        var respuesta = await _service.ReversarPagoManualAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    // ==================== POSTEO MISCEL�NEOS ====================



    [HttpGet("miscelaneos/{recibo}/detalle")]

    public async Task<IActionResult> GetDetalleReciboMiscelaneo(long recibo, CancellationToken ct)

    {

        var detalles = await _service.ObtenerDetalleReciboMiscelaneoAsync(recibo, ct);

        return Ok(detalles);

    }



    [HttpPost("miscelaneos/registrar")]

    public async Task<IActionResult> RegistrarPagoMiscelaneo([FromBody] PagoMiscelaneoCrearDto dto, CancellationToken ct)

    {

        var respuesta = await _service.RegistrarPagoMiscelaneoAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.CaptacionPagos, PermissionAction.Edit)]

    [HttpPost("miscelaneos/reverso")]

    public async Task<IActionResult> ReversarPagoMiscelaneo([FromBody] ReversoMiscelaneoRequestDto dto, CancellationToken ct)

    {

        var respuesta = await _service.ReversarPagoMiscelaneoAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    // ==================== COMBOS Y AUXILIARES ====================



    [HttpGet("clientes")]

    public async Task<IActionResult> GetClientes([FromQuery] string? query, [FromQuery] int? take, CancellationToken ct)

    {

        var clientes = await _service.ListarClientesAsync(query, take, ct);

        return Ok(clientes);

    }



    [HttpGet("bancos")]

    public async Task<IActionResult> GetBancos(CancellationToken ct)

    {

        var bancos = await _service.ListarBancosAsync(ct);

        return Ok(bancos);

    }



    [HttpGet("periodo-actual")]

    public async Task<IActionResult> GetPeriodoActual(CancellationToken ct)

    {

        var periodo = await _service.ObtenerPeriodoActualAsync(ct);

        return Ok(periodo);

    }

}



