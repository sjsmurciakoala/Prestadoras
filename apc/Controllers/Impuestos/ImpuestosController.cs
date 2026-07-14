using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Impuestos;
using SIAD.Services.Impuestos;
using apc.Security;

namespace apc.Controllers.Impuestos;

/// <summary>
/// Mantenimiento del catálogo GLOBAL de impuestos y sus tasas con vigencia.
/// <para>
/// Módulo de permisos: <c>configuracion</c>. Es el mismo que usa
/// <see cref="apc.Controllers.TiposDocumentoFiscalController"/>, el otro catálogo fiscal
/// global del repo (cfg_tipo_documento_fiscal). Los permisos module.configuracion.[view|
/// create|edit|delete] ya existen en <see cref="PermissionNames"/> y sus políticas ya
/// están registradas: el fallback a nivel de módulo de ModuleAuthorize resuelve
/// GET→View, POST→Create, PUT→Edit, DELETE→Delete sin necesidad de entradas nuevas en
/// PermissionEndpointCatalog (ese catálogo solo cubre ventas y contabilidad).
/// </para>
/// </summary>
[ApiController]
[Route("api/impuestos")]
[ModuleAuthorize(PermissionModules.Configuracion)]
public sealed class ImpuestosController : ControllerBase
{
    private readonly IImpuestosService _service;

    public ImpuestosController(IImpuestosService service) => _service = service;

    private string Usuario => User?.Identity?.Name ?? "system";

    // ----------------------------------------------------------------- impuesto

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] ImpuestoFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var x = await _service.GetByIdAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    /// <summary>El impuesto con todas sus tasas (vigentes e históricas).</summary>
    [HttpGet("{id:int}/detalle")]
    public async Task<IActionResult> GetDetalle(int id, CancellationToken ct)
    {
        var x = await _service.GetDetalleAsync(id, ct);
        return x is null ? NotFound() : Ok(x);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ImpuestoEditDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        try
        {
            var creado = await _service.CreateAsync(dto, Usuario, ct);
            return CreatedAtAction(nameof(GetById), new { id = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] ImpuestoEditDto dto, CancellationToken ct)
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

    // --------------------------------------------------------------------- tasas

    [HttpGet("{impuestoId:int}/tasas")]
    public async Task<IActionResult> GetTasas(int impuestoId, CancellationToken ct)
        => Ok(await _service.GetTasasAsync(impuestoId, ct));

    [HttpGet("tasas/{tasaId:int}")]
    public async Task<IActionResult> GetTasaById(int tasaId, CancellationToken ct)
    {
        var x = await _service.GetTasaByIdAsync(tasaId, ct);
        return x is null ? NotFound() : Ok(x);
    }

    /// <summary>
    /// Tasas que rigen a una fecha dada (por defecto hoy). Es lo que debe consultar el
    /// motor de cálculo: nunca "la tasa actual", siempre la que regía en la fecha del
    /// documento.
    /// </summary>
    [HttpGet("tasas/vigentes")]
    public async Task<IActionResult> GetTasasVigentes([FromQuery] DateOnly? fecha, CancellationToken ct)
        => Ok(await _service.GetTasasVigentesAsync(fecha ?? DateOnly.FromDateTime(DateTime.Today), ct));

    [HttpPost("tasas")]
    public async Task<IActionResult> CreateTasa([FromBody] ImpuestoTasaDto dto, CancellationToken ct)
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
    public async Task<IActionResult> UpdateTasa(int tasaId, [FromBody] ImpuestoTasaDto dto, CancellationToken ct)
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
    /// Cambio de tasa por decreto (transaccional): cierra la vigencia de la tasa actual
    /// y crea la nueva al día siguiente. NO es una edición — el histórico queda intacto.
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
}
