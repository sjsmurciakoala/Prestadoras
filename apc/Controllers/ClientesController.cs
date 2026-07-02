using System.Collections.Generic;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

using SIAD.Core.DTOs.Clientes;

using SIAD.Core.Constants;

using SIAD.Core.Tenancy;

using SIAD.Services.Clientes;

using apc.Data;
using apc.Security;



namespace apc.Controllers;



[ApiController]

[Route("api/[controller]")]

[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Clientes)]

public class ClientesController : ControllerBase

{

    private readonly IClientesService _clientesService;

    private readonly ICurrentCompanyService _currentCompanyService;

    private readonly UserManager<ApplicationUser> _userManager;

    public ClientesController(
        IClientesService clientesService,
        ICurrentCompanyService currentCompanyService,
        UserManager<ApplicationUser> userManager)
    {
        _clientesService = clientesService;
        _currentCompanyService = currentCompanyService;
        _userManager = userManager;
    }


    [HttpGet("{id:int}/foto-medidor/header")]

    public async Task<IActionResult> GetFotoMedidorHeader(int id, CancellationToken cancellationToken)

    {

        var header = await _clientesService.GetFotoMedidorHeaderAsync(id, cancellationToken);

        return Ok(header);

    }



    [HttpGet("{id:int}/foto-medidor")]

    public async Task<IActionResult> GetFotoMedidor(

        int id,

        [FromQuery] DateTime? desde,

        [FromQuery] DateTime? hasta,

        CancellationToken cancellationToken)

    {

        var desdeValue = (desde ?? DateTime.Today.AddYears(-1)).Date;

        var hastaValue = (hasta ?? DateTime.Today).Date;



        var items = await _clientesService.GetFotoMedidorAsync(id, desdeValue, hastaValue, cancellationToken);

        return Ok(items);

    }



    [HttpGet("foto-medidor/{ide:int}/imagen")]

    public async Task<IActionResult> GetFotoMedidorImagen(int ide, CancellationToken cancellationToken)

    {

        var data = await _clientesService.GetFotoMedidorImagenAsync(ide, cancellationToken);

        if (data is null || data.Length == 0)

        {

            return NotFound();

        }



        return File(data, "image/jpeg");

    }



    [HttpGet("{id:int}/estado-cuenta")]

    public async Task<IActionResult> GetEstadoCuenta(int id, CancellationToken cancellationToken)

    {

        var estadoCuenta = await _clientesService.GetEstadoCuentaAsync(id, cancellationToken);

        return Ok(estadoCuenta);

    }



    [HttpGet("{id:int}/movimientos")]

    public async Task<IActionResult> GetMovimientos(int id, CancellationToken cancellationToken)

    {

        var movimientos = await _clientesService.GetMovimientosAsync(id, cancellationToken);

        return Ok(movimientos);

    }



    [HttpGet("{id:int}/movimientos/paged")]

    public async Task<IActionResult> GetMovimientosPaged(

        int id,

        [FromQuery] int skip,

        [FromQuery] int take,

        [FromQuery] string? sortField,

        [FromQuery] bool sortDesc,

        CancellationToken cancellationToken)

    {

        try

        {

            if (take <= 0)

            {

                take = 50;

            }



            if (take > 500)

            {

                take = 500;

            }



            var result = await _clientesService.GetMovimientosPagedAsync(id, skip, take, sortField, sortDesc, cancellationToken);

            return Ok(result);

        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)

        {

            return StatusCode(499);

        }

    }

    



    [HttpGet("{id:int}/historico-consumo")]

    public async Task<IActionResult> GetHistoricoConsumo(

        int id,

        [FromQuery] DateTime? desde,

        [FromQuery] DateTime? hasta,

        CancellationToken cancellationToken)

    {

        var desdeValue = (desde ?? DateTime.Today.AddMonths(-12)).Date;

        var hastaValue = (hasta ?? DateTime.Today).Date;



        if (desdeValue > hastaValue)

        {

            (desdeValue, hastaValue) = (hastaValue, desdeValue);

        }



        var historico = await _clientesService.GetHistoricoConsumoAsync(id, desdeValue, hastaValue, cancellationToken);

        return Ok(historico);

    }



    [HttpGet("{id:int}/historico-consumo/paged")]

    public async Task<IActionResult> GetHistoricoConsumoPaged(

        int id,

        [FromQuery] DateTime? desde,

        [FromQuery] DateTime? hasta,

        [FromQuery] int skip,

        [FromQuery] int take,

        [FromQuery] string? sortField,

        [FromQuery] bool sortDesc,

        CancellationToken cancellationToken)

    {

        // NOTE: Added paged endpoint to support server-side paging/sorting with remote summary.

        var desdeValue = (desde ?? DateTime.Today.AddMonths(-12)).Date;

        var hastaValue = (hasta ?? DateTime.Today).Date;



        if (desdeValue > hastaValue)

        {

            (desdeValue, hastaValue) = (hastaValue, desdeValue);

        }



        var result = await _clientesService.GetHistoricoConsumoPagedAsync(id, desdeValue, hastaValue, skip, take, sortField, sortDesc, cancellationToken);

        return Ok(result);

    }



    [HttpGet("search")]

    public async Task<IActionResult> Search([FromQuery] ClienteFilterDto filtro, CancellationToken cancellationToken)

    {

        var clientes = await _clientesService.SearchClientesAsync(filtro, cancellationToken);

        return Ok(clientes);

    }



    [HttpGet("search-paged")]

    public async Task<IActionResult> SearchPaged(

        [FromQuery] string? search,

        [FromQuery] bool soloActivos,

        [FromQuery] int skip,

        [FromQuery] int take,

        [FromQuery] string? sortField,

        [FromQuery] bool sortDesc,

        CancellationToken cancellationToken)

    {

        try

        {

            if (take <= 0)

            {

                take = 50;

            }



            if (take > 500)

            {

                take = 500;

            }



            var result = await _clientesService.SearchClientesPagedAsync(search, soloActivos, skip, take, sortField, sortDesc, cancellationToken);

            return Ok(result);

        }

        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)

        {

            return StatusCode(499);

        }

    }





    [HttpPost]

    public async Task<IActionResult> Post([FromBody] ClienteCreateDto dto, CancellationToken cancellationToken)

    {

        if (!ModelState.IsValid)

        {

            return ValidationProblem(ModelState);

        }



        try

        {

            var companyId = _currentCompanyService.GetCompanyId();

            if (companyId <= 0)

            {

                return Forbid();

            }



            var usuario = User?.Identity?.Name ?? "system";

            var response = await _clientesService.CrearClienteAsync(dto, usuario, cancellationToken);

            return CreatedAtAction(nameof(GetById), new { id = response.Id }, response);

        }

        catch (InvalidOperationException ex)

        {

            return Conflict(new { message = ex.Message });

        }

        catch (ArgumentException ex)

        {

            return BadRequest(new { message = ex.Message });

        }

    }





    [HttpGet]

    public async Task<IActionResult> Get(CancellationToken cancellationToken)

    {

        var clientes = await _clientesService.GetClientesAsync(cancellationToken);

        return Ok(clientes);

    }



    [HttpGet("{id:int}")]

    public async Task<IActionResult> GetById(int id, CancellationToken cancellationToken)

    {

        var cliente = await _clientesService.GetClienteAsync(id, cancellationToken);

        return cliente is null ? NotFound() : Ok(cliente);

    }



    [HttpPut("{id:int}")]

    public async Task<IActionResult> Put(int id, [FromBody] ClienteUpdateDto dto, CancellationToken cancellationToken)

    {

        if (!ModelState.IsValid)

        {

            return ValidationProblem(ModelState);

        }



        try

        {

            var companyId = _currentCompanyService.GetCompanyId();

            if (companyId <= 0)

            {

                return Forbid();

            }



            var usuario = User?.Identity?.Name ?? "system";

            var actualizado = await _clientesService.ActualizarClienteAsync(id, dto, usuario, cancellationToken);

            return Ok(actualizado);

        }

        catch (KeyNotFoundException)

        {

            return NotFound();

        }

        catch (InvalidOperationException ex)

        {

            return Conflict(new { message = ex.Message });

        }

        catch (ArgumentException ex)

        {

            return BadRequest(new { message = ex.Message });

        }

    }

    [HttpGet("{id:int}/estado-log")]
    public async Task<IActionResult> GetEstadoLog(int id, CancellationToken ct)
    {
        var clave = await _clientesService.GetClienteAsync(id, ct);
        if (clave is null)
            return NotFound();

        var log = await _clientesService.GetEstadoLogAsync(clave.Codigo, ct);
        return Ok(log);
    }

    [HttpPost("{clave}/no-cortable")]
    [Authorize(Policy = PermissionNames.Ventas.Clientes.EditarNoCortable)]
    public async Task<IActionResult> SetNoCortable(string clave, [FromBody] SetNoCortableRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "La contraseña es obligatoria." });

        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        var user = await _userManager.FindByNameAsync(username);
        if (user is null)
            return Unauthorized();

        var passwordValida = await _userManager.CheckPasswordAsync(user, request.Password);
        if (!passwordValida)
            return BadRequest(new { message = "Contraseña incorrecta." });

        try
        {
            await _clientesService.SetNoCortableAsync(clave, request.NoCortable, username, request.Motivo, ct);
            return Ok(new { success = true, noCortable = request.NoCortable });
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

}


