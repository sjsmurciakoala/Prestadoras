using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Services.Proveedores;
using apc.Security;

namespace apc.Controllers.Proveedores;

/// <summary>
/// Incidencias de recepción (F4): devoluciones, daños, especificación distinta y faltantes.
/// <para>
/// Módulo <c>proveedores</c>, recurso FINO <c>incidencias</c>. La política acepta también los
/// permisos de inventario: quien recibe la mercadería es quien detecta y anota la incidencia.
/// </para>
/// </summary>
[ApiController]
[Route("api/proveedores/incidencias")]
[ModuleAuthorize(PermissionModules.Proveedores, PermissionResources.Proveedores.Incidencias)]
public sealed class RecepcionIncidenciaController : ControllerBase
{
    private readonly IRecepcionIncidenciaService _service;

    public RecepcionIncidenciaController(IRecepcionIncidenciaService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> Get(
        [FromQuery] string? codProveedor,
        [FromQuery] DateOnly? desde,
        [FromQuery] DateOnly? hasta,
        [FromQuery] short? tipo,
        [FromQuery] int? compraHdrId,
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var filtro = new RecepcionIncidenciaFilterDto
        {
            CodProveedor = codProveedor,
            FechaDesde = desde,
            FechaHasta = hasta,
            Tipo = tipo,
            CompraHdrId = compraHdrId,
            Search = search
        };

        return Ok(await _service.GetAsync(filtro, ct));
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var incidencia = await _service.GetByIdAsync(id, ct);
        return incidencia is null
            ? NotFound(new { mensaje = "La incidencia no existe." })
            : Ok(incidencia);
    }

    /// <summary>Recepciones no anuladas del proveedor, para elegir a cuál se le registra.</summary>
    [HttpGet("recepciones/{codProveedor}")]
    public async Task<IActionResult> GetRecepciones(
        string codProveedor, [FromQuery] string? search, CancellationToken ct)
        => Ok(await _service.BuscarRecepcionesAsync(codProveedor, search, ct));

    [HttpPost]
    public async Task<IActionResult> Crear(
        [FromBody] RecepcionIncidenciaUpsertDto dto, CancellationToken ct)
    {
        try
        {
            var creada = await _service.CrearAsync(dto, Usuario(), ct);
            return CreatedAtAction(nameof(GetById), new { id = creada.Id }, creada);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(
        int id, [FromBody] RecepcionIncidenciaUpsertDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ActualizarAsync(id, dto, Usuario(), ct));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { mensaje = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Eliminar(int id, CancellationToken ct)
        => await _service.EliminarAsync(id, ct)
            ? NoContent()
            : NotFound(new { mensaje = "La incidencia no existe." });

    private string Usuario() => User?.Identity?.Name ?? "sistema";
}
