using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Services.Presupuesto;

namespace apc.Controllers.Presupuesto;

[ApiController]
[Route("api/presupuesto/configuraciones")]
[Authorize(Policy = AuthorizationPolicies.Contabilidad)]
public sealed class ConfiguracionPresupuestoController : ControllerBase
{
    private readonly IConfiguracionPresupuestoService _service;

    public ConfiguracionPresupuestoController(IConfiguracionPresupuestoService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ConfiguracionPresupuestoFilterDto? filtro, CancellationToken ct)
    {
        var result = await _service.GetAsync(filtro, ct);
        return Ok(result);
    }

    [HttpGet("{idPresupuesto}/detalles")]
    public async Task<IActionResult> GetDetails(string idPresupuesto, CancellationToken ct)
    {
        var result = await _service.GetDetailsByPresupuestoAsync(idPresupuesto, ct);
        return Ok(result);
    }

    [HttpGet("{idPresupuesto}/detalles/{cuentaContable}")]
    public async Task<IActionResult> GetDetailById(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetDetailByIdAsync(idPresupuesto, cuentaContable, ct);
            return result is null ? NotFound() : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("{idPresupuesto}/detalles/{cuentaContable}/cuentas-destino-traslado")]
    public async Task<IActionResult> GetCuentasDestinoTraslado(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetCuentasDestinoTrasladoAsync(idPresupuesto, cuentaContable, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{idPresupuesto}/detalles/{cuentaContable}/solicitudes")]
    public async Task<IActionResult> GetSolicitudesByDetalle(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct)
    {
        try
        {
            var result = await _service.GetSolicitudesByDetalleAsync(idPresupuesto, cuentaContable, ct);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{idPresupuesto}/detalles/{cuentaContable}/solicitudes")]
    public async Task<IActionResult> PostSolicitudDetalle(
        string idPresupuesto,
        string cuentaContable,
        [FromBody] PresupuestoActividadSolicitudCreateDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.CreateSolicitudAsync(idPresupuesto, cuentaContable, dto, user, ct);
            return CreatedAtAction(
                nameof(GetSolicitudesByDetalle),
                new { idPresupuesto, cuentaContable },
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{idPresupuesto}/detalles/{cuentaContable}/solicitudes/{solicitudId:long}/aprobar")]
    public async Task<IActionResult> ApproveSolicitudDetalle(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        [FromBody] PresupuestoActividadSolicitudDecisionDto? dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.ApproveSolicitudAsync(
                idPresupuesto,
                cuentaContable,
                solicitudId,
                user,
                dto?.Comentario,
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{idPresupuesto}/detalles/{cuentaContable}/solicitudes/{solicitudId:long}/rechazar")]
    public async Task<IActionResult> RejectSolicitudDetalle(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        [FromBody] PresupuestoActividadSolicitudDecisionDto? dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.RejectSolicitudAsync(
                idPresupuesto,
                cuentaContable,
                solicitudId,
                user,
                dto?.Comentario,
                ct);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{idPresupuesto}/detalles")]
    public async Task<IActionResult> PostDetail(
        string idPresupuesto,
        [FromBody] ConfiguracionPresupuestoDetalleEditDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.AddDetailAsync(idPresupuesto, dto, user, ct);
            return CreatedAtAction(
                nameof(GetDetails),
                new { idPresupuesto },
                result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{idPresupuesto}/detalles/{cuentaContable}")]
    public async Task<IActionResult> PutDetail(
        string idPresupuesto,
        string cuentaContable,
        [FromBody] ConfiguracionPresupuestoDetalleUpdateDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.UpdateDetailAsync(idPresupuesto, cuentaContable, dto, user, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("{idPresupuesto}")]
    public async Task<IActionResult> GetByIdHeader(string idPresupuesto, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(idPresupuesto, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{idPresupuesto}/{cuentaContable}")]
    public async Task<IActionResult> GetByIdLegacy(string idPresupuesto, string cuentaContable, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(idPresupuesto, cuentaContable, ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("server-date")]
    public IActionResult GetServerDate()
    {
        var serverDate = DateOnly.FromDateTime(DateTime.Today);
        return Ok(new { serverDate });
    }

    [HttpGet("next-id")]
    public async Task<IActionResult> GetNextId([FromQuery] string? cuentaContable, CancellationToken ct)
    {
        try
        {
            var nextId = string.IsNullOrWhiteSpace(cuentaContable)
                ? await _service.GetNextIdAsync(ct)
                : await _service.GetNextIdAsync(cuentaContable, ct);
            return Ok(new { nextId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpGet("paged")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] ConfiguracionPresupuestoFilterDto? filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken ct)
    {
        var result = await _service.GetPagedAsync(filtro, skip, take, sortField, sortDesc, ct);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ConfiguracionPresupuestoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.CreateAsync(dto, user, ct);
            return CreatedAtAction(
                nameof(GetByIdHeader),
                new
                {
                    idPresupuesto = result.IdPresupuesto
                },
                result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{idPresupuesto}")]
    public async Task<IActionResult> PutById(
        string idPresupuesto,
        [FromBody] ConfiguracionPresupuestoEditDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.UpdateAsync(idPresupuesto, dto, user, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPost("{idPresupuesto}/aprobar")]
    [Authorize(Policy = AuthorizationPolicies.PresupuestoAprobacion)]
    public async Task<IActionResult> ApprovePresupuesto(string idPresupuesto, CancellationToken ct)
    {
        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.ApprovePresupuestoAsync(idPresupuesto, user, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpPut("{idPresupuesto}/{cuentaContable}")]
    public async Task<IActionResult> PutLegacy(
        string idPresupuesto,
        string cuentaContable,
        [FromBody] ConfiguracionPresupuestoEditDto dto,
        CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var user = User?.Identity?.Name ?? "system";
            var result = await _service.UpdateAsync(idPresupuesto, cuentaContable, dto, user, ct);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }

    [HttpDelete("{idPresupuesto}/{cuentaContable}")]
    public async Task<IActionResult> Delete(string idPresupuesto, string cuentaContable, CancellationToken ct)
    {
        try
        {
            var deleted = await _service.DeleteAsync(idPresupuesto, cuentaContable, ct);
            return deleted ? Ok(new { success = true }) : NotFound();
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { message = ex.Message });
        }
    }
}
