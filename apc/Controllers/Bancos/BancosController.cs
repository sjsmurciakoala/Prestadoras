using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BancosController : ControllerBase
{
    private readonly IBancosService service;

    public BancosController(IBancosService service)
    {
        this.service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] BancoFilterDto filtro, CancellationToken ct)
    {
        var bancos = await service.GetAsync(filtro, ct);
        return Ok(bancos);
    }

    [HttpGet("{bancoId:long}")]
    public async Task<IActionResult> GetById(long bancoId, CancellationToken ct)
    {
        var banco = await service.GetByIdAsync(bancoId, ct);
        return banco is null ? NotFound() : Ok(banco);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] BancoCreateDto dto, CancellationToken ct)
    {
        var user = User?.Identity?.Name ?? "system";
        try
        {
            var created = await service.CreateAsync(dto, user, ct);
            return CreatedAtAction(nameof(GetById), new { bancoId = created.BancoId }, created);
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
            return Conflict("No fue posible guardar el banco.");
        }
    }

    [HttpPut("{bancoId:long}")]
    public async Task<IActionResult> Put(long bancoId, [FromBody] BancoEditDto dto, CancellationToken ct)
    {
        if (dto.BancoId != 0 && dto.BancoId != bancoId)
        {
            return BadRequest("El banco no coincide con el identificador solicitado.");
        }

        var user = User?.Identity?.Name ?? "system";
        try
        {
            var updated = await service.UpdateAsync(bancoId, dto, user, ct);
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
            return Conflict("No fue posible guardar el banco.");
        }
    }

    [HttpDelete("{bancoId:long}")]
    public async Task<IActionResult> Delete(long bancoId, CancellationToken ct)
    {
        try
        {
            var deleted = await service.DeleteAsync(bancoId, ct);
            return deleted ? Ok() : NotFound();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(ex.Message);
        }
    }
}

