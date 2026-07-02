using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Services.Proveedores;
using apc.Security;
using SIAD.Core.Constants;

namespace apc.Controllers;

[ApiController]
[Route("api/[controller]")]
[ModuleAuthorize(PermissionModules.Proveedores)]
public class ProveedoresController : ControllerBase
{
    private readonly IProveedoresService _proveedoresService;

    public ProveedoresController(IProveedoresService proveedoresService)
    {
        _proveedoresService = proveedoresService;
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] ProveedorFilterDto filtro, CancellationToken cancellationToken)
    {
        var proveedores = await _proveedoresService.SearchProveedoresAsync(filtro, cancellationToken);
        return Ok(proveedores);
    }

    [HttpGet("tipos")]
    public async Task<IActionResult> GetTipos(CancellationToken cancellationToken)
    {
        var tipos = await _proveedoresService.GetTiposAsync(cancellationToken);
        return Ok(tipos);
    }

    [HttpGet("bancos")]
    public async Task<IActionResult> GetBancos(CancellationToken cancellationToken)
    {
        var bancos = await _proveedoresService.GetBancosAsync(cancellationToken);
        return Ok(bancos);
    }

    [HttpGet("bancos/catalogo")]
    public async Task<IActionResult> GetBancosCatalogo(CancellationToken cancellationToken)
    {
        try
        {
            var bancos = await _proveedoresService.GetBancosCatalogoAsync(cancellationToken);
            return Ok(bancos);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpGet("bancos/{id:long}")]
    public async Task<IActionResult> GetBanco(long id, CancellationToken cancellationToken)
    {
        try
        {
            var banco = await _proveedoresService.GetBancoAsync(id, cancellationToken);
            return banco is null ? NotFound() : Ok(banco);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpPost("bancos")]
    public async Task<IActionResult> PostBanco([FromBody] ProveedorBancoUpsertDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var id = await _proveedoresService.CreateBancoAsync(dto, usuario, cancellationToken);
            return CreatedAtAction(nameof(GetBanco), new { id }, null);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpPut("bancos/{id:long}")]
    public async Task<IActionResult> PutBanco(long id, [FromBody] ProveedorBancoUpsertDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            await _proveedoresService.UpdateBancoAsync(id, dto, usuario, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpDelete("bancos/{id:long}")]
    public async Task<IActionResult> DeleteBanco(long id, CancellationToken cancellationToken)
    {
        try
        {
            await _proveedoresService.DeleteBancoAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var proveedores = await _proveedoresService.GetProveedoresAsync(cancellationToken);
        return Ok(proveedores);
    }

    [HttpGet("{codigo}")]
    public async Task<IActionResult> GetByCodigo(string codigo, CancellationToken cancellationToken)
    {
        var proveedor = await _proveedoresService.GetProveedorAsync(codigo, cancellationToken);
        return proveedor is null ? NotFound() : Ok(proveedor);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ProveedorUpsertDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            var codigo = await _proveedoresService.CreateAsync(dto, usuario, cancellationToken);
            return CreatedAtAction(nameof(GetByCodigo), new { codigo }, new { codigo });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpPut("{codigo}")]
    public async Task<IActionResult> Put(string codigo, [FromBody] ProveedorUpsertDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var usuario = User?.Identity?.Name ?? "system";
            await _proveedoresService.UpdateAsync(codigo, dto, usuario, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    [HttpDelete("{codigo}")]
    public async Task<IActionResult> Delete(string codigo, CancellationToken cancellationToken)
    {
        try
        {
            await _proveedoresService.DeleteAsync(codigo, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    [HttpGet("tipos/catalogo")]
    public async Task<IActionResult> GetTiposCatalogo(CancellationToken cancellationToken)
    {
        var tipos = await _proveedoresService.GetTiposCatalogoAsync(cancellationToken);
        return Ok(tipos);
    }

    [HttpGet("tipos/{id:int}")]
    public async Task<IActionResult> GetTipo(int id, CancellationToken cancellationToken)
    {
        var tipo = await _proveedoresService.GetTipoAsync(id, cancellationToken);
        return tipo is null ? NotFound() : Ok(tipo);
    }

    [HttpPost("tipos")]
    public async Task<IActionResult> PostTipo([FromBody] TipoProveedorUpsertDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var id = await _proveedoresService.CreateTipoAsync(dto, cancellationToken);
            return CreatedAtAction(nameof(GetTipo), new { id }, null);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpPut("tipos/{id:int}")]
    public async Task<IActionResult> PutTipo(int id, [FromBody] TipoProveedorUpsertDto dto, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            await _proveedoresService.UpdateTipoAsync(id, dto, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }

    [HttpDelete("tipos/{id:int}")]
    public async Task<IActionResult> DeleteTipo(int id, CancellationToken cancellationToken)
    {
        try
        {
            await _proveedoresService.DeleteTipoAsync(id, cancellationToken);
            return NoContent();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { detail = ex.Message });
        }
    }
}



