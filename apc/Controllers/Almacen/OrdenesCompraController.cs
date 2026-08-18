using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using apc.Security;

namespace apc.Controllers.Almacen;

/// <summary>
/// Órdenes de compra a proveedores. La orden nace en Borrador, se aprueba y luego la
/// recepción (factura de proveedor) la consume. Thin: valida, resuelve usuario y delega.
/// <para>
/// Aprobar y anular son POST pero se autorizan como <see cref="PermissionAction.Edit"/>:
/// cambian el estado de un documento existente, no crean uno nuevo (decisión del usuario
/// 2026-07-30: la aprobación se controla por permiso de rol, no por jerarquía).
/// </para>
/// </summary>
[ApiController]
[Route("api/almacen/ordenes-compra")]
[ModuleAuthorize(PermissionModules.Compras)]
public sealed class OrdenesCompraController : ControllerBase
{
    private readonly IOrdenCompraService _service;

    public OrdenesCompraController(IOrdenCompraService service)
    {
        _service = service;
    }

    private string Usuario => User?.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] OrdenCompraFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    /// <summary>Artículos comprables a un proveedor (existen en almacén con su código de proveedor).</summary>
    [HttpGet("articulos-proveedor")]
    public async Task<IActionResult> GetArticulosProveedor(
        [FromQuery] string codProveedor, [FromQuery] string? search, CancellationToken ct)
        => Ok(await _service.BuscarArticulosProveedorAsync(codProveedor, search, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var orden = await _service.GetByIdAsync(id, ct);
        return orden is null ? NotFound() : Ok(orden);
    }

    /// <summary>Comprobante de la orden de compra en PDF (inline), para autorizar y enviar al proveedor.</summary>
    [HttpGet("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePdf(int id, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(id, Usuario, ct);
        if (datos is null)
        {
            return NotFound(new { message = $"No se encontró la orden de compra {id}." });
        }

        using var report = new Rpt_Dev_Comprobante_OrdenCompra(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=OrdenCompra-{datos.Documento.Numero:00000}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] OrdenCompraDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            var creada = await _service.CrearAsync(dto, Usuario, ct);
            return Ok(creada);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex)
        {
            // Red de seguridad: el servicio ya acota rangos, pero un CHECK/FK de la BD que se
            // escape no debe salir como 500 con la página de error.
            return Problem(detail: MensajeDeBd(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Actualizar(int id, [FromBody] OrdenCompraDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        try
        {
            return Ok(await _service.ActualizarAsync(id, dto, Usuario, ct));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex)
        {
            return Problem(detail: MensajeDeBd(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Mensaje accionable para el usuario sin filtrar el detalle interno de Postgres.</summary>
    private static string MensajeDeBd(DbUpdateException ex)
        => ex.InnerException is PostgresException pg && pg.SqlState == "22003"
            ? "Alguno de los importes o cantidades está fuera del rango permitido."
            : "No se pudo guardar la orden de compra: revise los datos e intente de nuevo.";

    [HttpPost("{id:int}/aprobar")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> Aprobar(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _service.AprobarAsync(id, Usuario, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    [HttpPost("{id:int}/anular")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> Anular(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _service.AnularAsync(id, Usuario, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
