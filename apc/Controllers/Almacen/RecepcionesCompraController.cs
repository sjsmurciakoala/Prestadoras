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
/// Recepción de compras (factura de proveedor): el documento que entra mercadería al
/// inventario. Thin: valida, resuelve usuario y delega.
/// <para>
/// Una O/C admite varias facturas, así que el alta es la operación central y no hay edición:
/// una factura registrada ya movió el kardex, y el kardex es inmutable. Corregir será anular
/// (reversa) y volver a capturar.
/// </para>
/// </summary>
[ApiController]
[Route("api/almacen/recepciones-compra")]
[ModuleAuthorize(PermissionModules.Compras)]
public sealed class RecepcionesCompraController : ControllerBase
{
    private readonly IRecepcionCompraService _service;

    public RecepcionesCompraController(IRecepcionCompraService service)
    {
        _service = service;
    }

    private string Usuario => User?.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] RecepcionCompraFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var recepcion = await _service.GetByIdAsync(id, ct);
        return recepcion is null ? NotFound() : Ok(recepcion);
    }

    /// <summary>Comprobante de la factura de compra (recepción) en PDF (inline).</summary>
    [HttpGet("{id:int}/comprobante/pdf")]
    public async Task<IActionResult> GetComprobantePdf(int id, CancellationToken ct)
    {
        var datos = await _service.GetDatosImpresionAsync(id, Usuario, ct);
        if (datos is null)
        {
            return NotFound(new { message = $"No se encontró la factura de compra {id}." });
        }

        using var report = new Rpt_Dev_Comprobante_RecepcionCompra(datos);
        using var stream = new MemoryStream();
        report.ExportToPdf(stream);

        Response.Headers.ContentDisposition = $"inline; filename=FacturaCompra-{datos.Documento.Numero:00000}.pdf";
        return File(stream.ToArray(), "application/pdf");
    }

    /// <summary>Órdenes de compra que todavía admiten recepción (Aprobadas o Recibidas parcialmente).</summary>
    [HttpGet("ordenes-recibibles")]
    public async Task<IActionResult> GetOrdenesRecibibles([FromQuery] string? codProveedor, CancellationToken ct)
        => Ok(await _service.ObtenerOrdenesRecibiblesAsync(codProveedor, ct));

    /// <summary>Renglones de una O/C con lo que falta por recibir (pedida − aplicada).</summary>
    [HttpGet("ordenes/{ordenCompraId:int}/pendientes")]
    public async Task<IActionResult> GetPendientes(int ordenCompraId, CancellationToken ct)
        => Ok(await _service.ObtenerPendientesOrdenAsync(ordenCompraId, ct));

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] RecepcionCompraDto dto, CancellationToken ct)
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
            // Red de seguridad: el servicio ya acota rangos y valida reglas, pero un
            // CHECK/índice de la BD que se escape no debe salir como 500.
            return Problem(detail: MensajeDeBd(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Anula la factura: revierte sus asientos en el kardex y devuelve lo recibido a la O/C.
    /// POST pero se autoriza como <see cref="PermissionAction.Edit"/>: cambia el estado de un
    /// documento existente, no crea uno nuevo (mismo criterio que aprobar/anular una O/C).
    /// </summary>
    [HttpPost("{id:int}/anular")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> Anular(int id, [FromBody] AnularRecepcionRequest? request, CancellationToken ct)
    {
        try
        {
            var ok = await _service.AnularAsync(id, request?.Motivo, Usuario, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (DbUpdateException ex)
        {
            return Problem(detail: MensajeDeBd(ex), statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Cuerpo de la anulación. El motivo es opcional y queda en las observaciones.</summary>
    public sealed class AnularRecepcionRequest
    {
        public string? Motivo { get; set; }
    }

    /// <summary>Mensaje accionable sin filtrar el detalle interno de Postgres.</summary>
    private static string MensajeDeBd(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pg)
        {
            return "No se pudo registrar la factura: revise los datos e intente de nuevo.";
        }

        return pg.SqlState switch
        {
            "22003" => "Alguno de los importes o cantidades está fuera del rango permitido.",
            "23505" when pg.ConstraintName == "uq_alm_compra_hdr_factura_prov" =>
                "Esa factura ya fue registrada para este proveedor.",
            "23505" when pg.ConstraintName == "uq_alm_compra_hdr_numero" =>
                "El correlativo de recepción se topó con otro en curso: intente de nuevo.",
            _ => "No se pudo registrar la factura: revise los datos e intente de nuevo."
        };
    }
}
