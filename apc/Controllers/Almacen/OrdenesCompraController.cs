using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Reports;
using SIAD.Services.Almacen;
using SIAD.Services.Aprobaciones;
using SIAD.Services.Presupuesto;
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
    private readonly IPresupuestoCompromisoService _presupuesto;
    private readonly IAprobacionService _aprobacion;

    public OrdenesCompraController(
        IOrdenCompraService service, IPresupuestoCompromisoService presupuesto,
        IAprobacionService aprobacion)
    {
        _service = service;
        _presupuesto = presupuesto;
        _aprobacion = aprobacion;
    }

    private string Usuario => User?.Identity?.Name ?? "system";

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] OrdenCompraFilterDto filtro, CancellationToken ct)
        => Ok(await _service.GetAsync(filtro, ct));

    /// <summary>
    /// Cómo quedaría el presupuesto si se aprobara esta orden. Lectura informativa: la validación
    /// real ocurre al aprobar, bajo lock. Devuelve Modo = 0 si el control está apagado.
    /// </summary>
    [HttpGet("{id:int}/presupuesto")]
    public async Task<IActionResult> GetPresupuesto(int id, CancellationToken ct)
    {
        var orden = await _service.GetByIdAsync(id, ct);
        if (orden is null) return NotFound();

        var fecha = orden.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        return Ok(await _presupuesto.ConsultarPrevioOrdenCompraAsync(id, fecha, ct));
    }

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

    /// <summary>
    /// Aprueba la orden. Con la escalera apagada es el flujo histórico de un clic; con la escalera
    /// encendida equivale a firmar el nivel pendiente y <b>exige el permiso propio</b>.
    /// </summary>
    [HttpPost("{id:int}/aprobar")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> Aprobar(int id, CancellationToken ct)
    {
        if (await EscaleraEncendidaAsync(ct) && !PuedeAprobar)
        {
            return SinPermisoParaFirmar();
        }

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

    /// <summary>
    /// Envía una orden en Borrador a la escalera de firmas. Es una acción de quien PREPARA la
    /// orden, no de quien la aprueba: por eso pide <c>Edit</c> y no el permiso de firmar.
    /// </summary>
    [HttpPost("{id:int}/enviar-aprobacion")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> EnviarAAprobacion(int id, CancellationToken ct)
    {
        try
        {
            var ok = await _service.EnviarAAprobacionAsync(id, Usuario, ct);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>
    /// Firma el nivel pendiente. Devuelve qué pasó (nivel firmado, si completó la escalera, si
    /// reservó presupuesto) para que la pantalla lo diga sin adivinar.
    /// </summary>
    [HttpPost("{id:int}/firmar")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public async Task<IActionResult> Firmar(int id, [FromBody] ComentarioDto? dto, CancellationToken ct)
    {
        if (!PuedeAprobar) return SinPermisoParaFirmar();

        try
        {
            var resultado = await _service.FirmarAprobacionAsync(id, dto?.Comentario, Usuario, ct);
            return resultado is null ? NotFound() : Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }

    /// <summary>Devuelve la orden a Borrador: borra las firmas y libera lo reservado.</summary>
    [HttpPost("{id:int}/devolver")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public Task<IActionResult> Devolver(int id, [FromBody] MotivoDto dto, CancellationToken ct)
    {
        if (!PuedeAprobar) return Task.FromResult(SinPermisoParaFirmar());
        return EjecutarConMotivoAsync(dto, motivo => _service.DevolverABorradorAsync(id, motivo, Usuario, ct));
    }

    /// <summary>Estado de la escalera de una orden: niveles, firmas y si el usuario puede firmar.</summary>
    [HttpGet("{id:int}/aprobaciones")]
    public async Task<IActionResult> Aprobaciones(int id, CancellationToken ct)
        => Ok(await _aprobacion.ObtenerEstadoAsync(DocumentosAprobacion.OrdenCompra, id, ct));

    /// <summary>Bandeja "Mis aprobaciones": órdenes esperando la firma del usuario de la sesión.</summary>
    [HttpGet("pendientes-aprobacion")]
    public async Task<IActionResult> PendientesAprobacion(CancellationToken ct)
    {
        if (!PuedeAprobar) return SinPermisoParaFirmar();
        return Ok(await _aprobacion.PendientesOrdenCompraAsync(ct));
    }

    /// <summary>
    /// Qué botones tiene que ofrecer el listado: si la empresa exige escalera y si este usuario
    /// puede firmar. Una sola llamada por carga de pantalla, en vez de deducirlo fila por fila.
    /// </summary>
    [HttpGet("aprobacion-config")]
    public async Task<IActionResult> AprobacionConfig(CancellationToken ct)
        => Ok(new
        {
            Encendido = await EscaleraEncendidaAsync(ct),
            PuedoFirmar = PuedeAprobar
        });

    /// <summary>Progreso de las órdenes en la escalera, para el badge del listado.</summary>
    [HttpGet("aprobacion-progreso")]
    public async Task<IActionResult> AprobacionProgreso(CancellationToken ct)
        => Ok(await _aprobacion.ProgresoOrdenesCompraAsync(ct));

    /// <summary>Comentario opcional de la firma.</summary>
    public sealed class ComentarioDto
    {
        public string? Comentario { get; set; }
    }

    /// <summary>
    /// El permiso de firmar es propio y <b>sin fallback</b> a <c>compras.edit</c>: si lo tuviera,
    /// no separaría nada de lo que separa hoy. Mismo patrón que la requisición.
    /// </summary>
    private bool PuedeAprobar =>
        User.IsInRole(RoleNames.SuperAdministrador)
        || User.HasClaim(PermissionClaimTypes.Permission, PermissionNames.Compras.Ordenes.Aprobar);

    private IActionResult SinPermisoParaFirmar()
        => Problem(
            detail: "No tiene el permiso para aprobar órdenes de compra.",
            statusCode: StatusCodes.Status403Forbidden);

    /// <summary>
    /// Con la escalera APAGADA el permiso de firmar no se exige, para no dejar sin poder aprobar a
    /// quienes hoy lo hacen con <c>compras.edit</c>. El endurecimiento llega con el control
    /// encendido, que es cuando la empresa ya está configurando quién firma qué.
    /// </summary>
    private Task<bool> EscaleraEncendidaAsync(CancellationToken ct)
        => _aprobacion.RequiereAprobacionAsync(DocumentosAprobacion.OrdenCompra, ct);

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

    /// <summary>Rechaza una orden en Borrador. No hay presupuesto que liberar.</summary>
    [HttpPost("{id:int}/rechazar")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public Task<IActionResult> Rechazar(int id, [FromBody] MotivoDto dto, CancellationToken ct)
        => EjecutarConMotivoAsync(dto, motivo => _service.RechazarAsync(id, motivo, Usuario, ct));

    /// <summary>
    /// Cancela una orden aprobada o recibida en parte: lo pendiente ya no se va a recibir y su
    /// presupuesto comprometido se libera.
    /// </summary>
    [HttpPost("{id:int}/cancelar")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public Task<IActionResult> Cancelar(int id, [FromBody] MotivoDto dto, CancellationToken ct)
        => EjecutarConMotivoAsync(dto, motivo => _service.CancelarAsync(id, motivo, Usuario, ct));

    /// <summary>Cierra anticipadamente una orden recibida en parte y libera el saldo comprometido.</summary>
    [HttpPost("{id:int}/cerrar")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Edit)]
    public Task<IActionResult> Cerrar(int id, [FromBody] MotivoDto dto, CancellationToken ct)
        => EjecutarConMotivoAsync(dto, motivo => _service.CerrarAsync(id, motivo, Usuario, ct));

    /// <summary>Motivo obligatorio de las transiciones que cierran una orden antes de tiempo.</summary>
    public sealed class MotivoDto
    {
        public string? Motivo { get; set; }
    }

    private async Task<IActionResult> EjecutarConMotivoAsync(MotivoDto? dto, Func<string, Task<bool>> accion)
    {
        var motivo = dto?.Motivo?.Trim();
        if (string.IsNullOrWhiteSpace(motivo))
        {
            return Problem(detail: "Indique el motivo.", statusCode: StatusCodes.Status400BadRequest);
        }

        try
        {
            var ok = await accion(motivo);
            return ok ? Ok(new { success = true }) : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
