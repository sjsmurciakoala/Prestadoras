using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

[ApiController]
[Route("api/almacen/articulos")]
[ModuleAuthorize(PermissionModules.Inventario)]
public sealed class ArticulosController : ControllerBase
{
    private readonly IArticulosService _service;
    private readonly IArticuloUbicacionService _ubicaciones;
    private readonly IArticuloProveedorService _proveedores;

    public ArticulosController(IArticulosService service, IArticuloUbicacionService ubicaciones, IArticuloProveedorService proveedores)
    {
        _service = service;
        _ubicaciones = ubicaciones;
        _proveedores = proveedores;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ArticuloFilterDto filtro, CancellationToken ct)
    {
        var articulos = await _service.GetAsync(filtro, ct);
        return Ok(articulos);
    }

    [HttpGet("alertas")]
    public async Task<IActionResult> GetAlertas([FromQuery] AlertaStockFilterDto filtro, CancellationToken ct)
    {
        var alertas = await _service.GetAlertasStockAsync(filtro, ct);
        return Ok(alertas);
    }

    [HttpGet("lineas")]
    public async Task<IActionResult> GetLineas(CancellationToken ct)
    {
        var lineas = await _service.GetLineasAsync(ct);
        return Ok(lineas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var articulo = await _service.GetByIdAsync(id, ct);
        return articulo is null ? NotFound() : Ok(articulo);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ArticuloEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User?.Identity?.Name ?? "system";
        try
        {
            var creado = await _service.CreateAsync(dto, usuario, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ArticuloEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User?.Identity?.Name ?? "system";
        var actualizado = await _service.UpdateAsync(id, dto, usuario, ct);
        return Ok(actualizado);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var ok = await _service.DeleteAsync(id, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    // ── Ubicaciones del artículo por bodega ──────────────────────────────────

    [HttpGet("{articuloId:int}/ubicaciones")]
    public async Task<IActionResult> GetUbicaciones(int articuloId, [FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _ubicaciones.GetAsync(articuloId, incluirInactivas, ct));

    [HttpPost("{articuloId:int}/ubicaciones")]
    public async Task<IActionResult> AddUbicacion(int articuloId, [FromBody] ArticuloUbicacionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _ubicaciones.AddAsync(articuloId, dto, User?.Identity?.Name ?? "system", ct);
            return Ok(creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{articuloId:int}/ubicaciones/{id:int}")]
    public async Task<IActionResult> UpdateUbicacion(int articuloId, int id, [FromBody] ArticuloUbicacionDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _ubicaciones.UpdateAsync(articuloId, id, dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // DELETE = deshabilitar (soft-delete): la ubicación se conserva para histórico.
    [HttpDelete("{articuloId:int}/ubicaciones/{id:int}")]
    public async Task<IActionResult> DeshabilitarUbicacion(int articuloId, int id, CancellationToken ct)
    {
        try
        {
            var ok = await _ubicaciones.DeshabilitarAsync(articuloId, id, User?.Identity?.Name ?? "system", ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{articuloId:int}/ubicaciones/{id:int}/reactivar")]
    public async Task<IActionResult> ReactivarUbicacion(int articuloId, int id, CancellationToken ct)
    {
        try
        {
            var ok = await _ubicaciones.ReactivarAsync(articuloId, id, User?.Identity?.Name ?? "system", ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // ── Proveedores del artículo ("UPC") ──────────────────────────────────────

    [HttpGet("{articuloId:int}/proveedores")]
    public async Task<IActionResult> GetProveedores(int articuloId, [FromQuery] bool incluirInactivas, CancellationToken ct)
        => Ok(await _proveedores.GetAsync(articuloId, incluirInactivas, ct));

    [HttpPost("{articuloId:int}/proveedores")]
    public async Task<IActionResult> AddProveedor(int articuloId, [FromBody] ArticuloProveedorDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _proveedores.AddAsync(articuloId, dto, User?.Identity?.Name ?? "system", ct);
            return Ok(creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{articuloId:int}/proveedores/{id:int}")]
    public async Task<IActionResult> UpdateProveedor(int articuloId, int id, [FromBody] ArticuloProveedorDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _proveedores.UpdateAsync(articuloId, id, dto, User?.Identity?.Name ?? "system", ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // DELETE = deshabilitar (soft-delete): la relación se conserva para histórico.
    [HttpDelete("{articuloId:int}/proveedores/{id:int}")]
    public async Task<IActionResult> DeshabilitarProveedor(int articuloId, int id, CancellationToken ct)
    {
        try
        {
            var ok = await _proveedores.DeshabilitarAsync(articuloId, id, User?.Identity?.Name ?? "system", ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{articuloId:int}/proveedores/{id:int}/reactivar")]
    public async Task<IActionResult> ReactivarProveedor(int articuloId, int id, CancellationToken ct)
    {
        try
        {
            var ok = await _proveedores.ReactivarAsync(articuloId, id, User?.Identity?.Name ?? "system", ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
