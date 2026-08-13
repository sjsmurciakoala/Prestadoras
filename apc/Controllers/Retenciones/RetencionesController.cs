using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Services.Retenciones;
using apc.Security;

namespace apc.Controllers.Retenciones;

/// <summary>
/// Mantenimiento del catálogo de retenciones a proveedores.
/// <para>
/// Módulo de permisos: <c>configuracion</c>, con recurso FINO <c>retenciones</c>
/// (<see cref="PermissionResources.Configuracion.Retenciones"/>). ModuleAuthorize mapea
/// GET→View, POST→Create, PUT→Edit y chequea en cascada:
/// <c>module.configuracion.retenciones.[view|create|edit]</c> → <c>module.configuracion.[...]</c>
/// → legacy. Así el permiso fino no bloquea a quien ya tiene el permiso de módulo.
/// </para>
/// <para>
/// El catálogo (concepto + tasas) es GLOBAL; la cuenta del pasivo (<c>{id}/cuenta</c>) es por
/// empresa (tenant), resuelta por <c>ICurrentCompanyService</c>.
/// </para>
/// </summary>
[ApiController]
[Route("api/retenciones")]
[ModuleAuthorize(PermissionModules.Configuracion, PermissionResources.Configuracion.Retenciones)]
public sealed class RetencionesController : ControllerBase
{
    private readonly IRetencionesService _service;

    public RetencionesController(IRetencionesService service) => _service = service;

    private string Usuario => User?.Identity?.Name ?? "system";

    // ---------------------------------------------------------------- retención

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RetencionFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    /// <summary>La retención con todas sus tasas y la cuenta configurada para la empresa actual.</summary>
    [HttpGet("{id:int}/detalle")]
    public async Task<IActionResult> GetDetalle(int id, CancellationToken ct)
    {
        var x = await _service.GetDetalleAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] RetencionEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creada = await _service.CreateAsync(dto, Usuario, ct);
            return CreatedAtAction(nameof(GetById), new { id = creada.Id }, creada);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] RetencionEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.UpdateAsync(id, dto, Usuario, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/desactivar")]
    public async Task<IActionResult> Desactivar(int id, CancellationToken ct)
    {
        var ok = await _service.DeactivateAsync(id, Usuario, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    // -------------------------------------------------------------------- tasas

    [HttpGet("{retencionId:int}/tasas")]
    public async Task<IActionResult> GetTasas(int retencionId, CancellationToken ct)
        => Ok(await _service.GetTasasAsync(retencionId, ct));

    [HttpGet("tasas/{tasaId:int}")]
    public async Task<IActionResult> GetTasaById(int tasaId, CancellationToken ct)
    {
        var x = await _service.GetTasaByIdAsync(tasaId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    /// <summary>
    /// Retenciones con la tasa que rige a una fecha dada (por defecto hoy). Es lo que consumirá F2:
    /// nunca "la tasa actual", siempre la que regía en la fecha del pago.
    /// </summary>
    [HttpGet("tasas/vigentes")]
    public async Task<IActionResult> GetTasasVigentes([FromQuery] DateOnly? fecha, CancellationToken ct)
        => Ok(await _service.GetTasasVigentesAsync(fecha ?? DateOnly.FromDateTime(DateTime.Today), ct));

    [HttpPost("tasas")]
    public async Task<IActionResult> CreateTasa([FromBody] RetencionTasaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creada = await _service.CreateTasaAsync(dto, Usuario, ct);
            return CreatedAtAction(nameof(GetTasaById), new { tasaId = creada.Id }, creada);
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("tasas/{tasaId:int}")]
    public async Task<IActionResult> UpdateTasa(int tasaId, [FromBody] RetencionTasaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.UpdateTasaAsync(tasaId, dto, Usuario, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("tasas/{tasaId:int}/desactivar")]
    public async Task<IActionResult> DesactivarTasa(int tasaId, CancellationToken ct)
    {
        var ok = await _service.DeactivateTasaAsync(tasaId, Usuario, ct);
        return ok ? Ok(new { success = true }) : NotFound();
    }

    /// <summary>
    /// Cambio de tasa por decreto (transaccional): cierra la vigencia de la tasa actual y crea la
    /// nueva al día siguiente. NO es una edición — el histórico queda intacto.
    /// </summary>
    [HttpPost("tasas/cambiar")]
    public async Task<IActionResult> CambiarTasa([FromBody] CambiarTasaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.CambiarTasaAsync(dto, Usuario, ct));
        }
        catch (KeyNotFoundException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status404NotFound);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    // --------------------------------------------- cuenta del pasivo por empresa

    /// <summary>Cuentas posteables del plan de la empresa actual, para el desplegable de selección.</summary>
    [HttpGet("cuentas-posteables")]
    public async Task<IActionResult> GetCuentasPosteables(CancellationToken ct)
        => Ok(await _service.GetCuentasPosteablesAsync(ct));

    /// <summary>La cuenta del pasivo configurada para esta retención en la empresa actual (o vacío).</summary>
    [HttpGet("{retencionId:int}/cuenta")]
    public async Task<IActionResult> GetCuenta(int retencionId, CancellationToken ct)
    {
        var x = await _service.GetCuentaAsync(retencionId, ct);
        return x is null ? NoContent() : Ok(x);
    }

    /// <summary>Configura (alta o actualización) la cuenta del pasivo para esta retención en la empresa actual.</summary>
    [HttpPut("{retencionId:int}/cuenta")]
    public async Task<IActionResult> SetCuenta(int retencionId, [FromBody] RetencionCuentaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            return Ok(await _service.SetCuentaAsync(retencionId, dto, Usuario, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
