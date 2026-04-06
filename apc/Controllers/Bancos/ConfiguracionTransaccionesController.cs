using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/configuracion-transacciones")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class ConfiguracionTransaccionesController : ControllerBase
{
    private readonly IBancoConfiguracionTransaccionesService service;

    public ConfiguracionTransaccionesController(IBancoConfiguracionTransaccionesService service)
    {
        this.service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] BancoConfiguracionTransaccionFilterDto filtro, CancellationToken ct)
    {
        var data = await service.GetAsync(filtro, ct);
        return Ok(data);
    }

    [HttpGet("{tipoTransaccionId}")]
    public async Task<IActionResult> GetById(string tipoTransaccionId, CancellationToken ct)
    {
        var item = await service.GetByIdAsync(tipoTransaccionId, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] BancoConfiguracionTransaccionEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var user = User?.Identity?.Name ?? "system";
        try
        {
            var created = await service.CreateAsync(dto, user, ct);
            return CreatedAtAction(nameof(GetById), new { tipoTransaccionId = created.TipoTransaccionId }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (DbUpdateException)
        {
            return Conflict("No fue posible guardar la configuracion de transacciones.");
        }
    }

    [HttpPut("{tipoTransaccionId}")]
    public async Task<IActionResult> Put(string tipoTransaccionId, [FromBody] BancoConfiguracionTransaccionEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!string.IsNullOrWhiteSpace(dto.TipoTransaccionId)
            && !string.Equals(dto.TipoTransaccionId.Trim(), tipoTransaccionId.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest("El codigo solicitado no coincide con el registro.");
        }

        var user = User?.Identity?.Name ?? "system";
        try
        {
            var updated = await service.UpdateAsync(tipoTransaccionId, dto, user, ct);
            return Ok(updated);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (DbUpdateException)
        {
            return Conflict("No fue posible actualizar la configuracion de transacciones.");
        }
    }

    [HttpDelete("{tipoTransaccionId}")]
    public async Task<IActionResult> Delete(string tipoTransaccionId, CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteAsync(tipoTransaccionId, ct);
            return deleted ? Ok() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
        catch (DbUpdateException)
        {
            return Conflict("No fue posible eliminar la configuracion de transacciones.");
        }
    }
}

