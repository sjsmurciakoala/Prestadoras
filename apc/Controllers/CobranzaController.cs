using Microsoft.AspNetCore.Authorization;

using Microsoft.AspNetCore.Mvc;

using SIAD.Core.DTOs.Cobranza;

using SIAD.Services.Cobranza;

using apc.Security;

using SIAD.Core.Constants;

using SIAD.Reports;



namespace apc.Controllers;



[ApiController]

[Route("api/[controller]")]

[ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Cobranza)]

public class CobranzaController : ControllerBase

{

    private readonly ICobranzaService _service;



    public CobranzaController(ICobranzaService service)

    {

        _service = service;

    }



    [HttpGet("clientes/{clave}/saldos")]

    public async Task<IActionResult> ObtenerSaldos(string clave, CancellationToken ct)

    {

        var saldos = await _service.ObtenerSaldosClienteAsync(clave, ct);

        return Ok(saldos);

    }



    [HttpGet("clientes/{clave}/bloqueo")]

    public async Task<IActionResult> ObtenerBloqueo(string clave, CancellationToken ct)

    {

        var bloqueado = await _service.EstaBloqueadoAsync(clave, ct);

        return Ok(new { Bloqueado = bloqueado });

    }



    [HttpGet("numero-letras")]

    public async Task<IActionResult> NumeroALetras([FromQuery] decimal valor, CancellationToken ct)

    {

        var letras = await _service.NumeroALetrasAsync(valor, ct);

        return Ok(new { Valor = valor, Texto = letras });

    }



    [ModuleAuthorize(PermissionModules.Ventas, PermissionResources.Ventas.Cobranza, PermissionAction.View)]

    [HttpPost("planes/calcular")]

    public async Task<IActionResult> CalcularPlan([FromBody] CobranzaPlanPreviewRequestDto dto, CancellationToken ct)

    {

        var preview = await _service.CalcularCuotasAsync(dto, ct);

        return Ok(preview);

    }



    [HttpPost("planes")]

    public async Task<IActionResult> GuardarPlan([FromBody] CobranzaPlanGuardarDto dto, CancellationToken ct)

    {

        var respuesta = await _service.GuardarPlanPagoAsync(dto, ct);

        if (!respuesta.Success)

        {

            return BadRequest(respuesta);

        }



        return Ok(respuesta);

    }



    [HttpGet("planes")]

    public async Task<IActionResult> ListarPlanes(CancellationToken ct)

    {

        var planes = await _service.ListarPlanesAsync(ct);

        return Ok(planes);

    }



    [HttpGet("planes/{correlativo}")]

    public async Task<IActionResult> ObtenerPlan(string correlativo, CancellationToken ct)

    {

        var plan = await _service.ObtenerPlanAsync(correlativo, ct);

        return plan is null ? NotFound() : Ok(plan);

    }



    // GET api/cobranza/clientes/{clave}/acciones
    [HttpGet("clientes/{clave}/acciones")]
    public async Task<IActionResult> ListarAcciones(string clave, CancellationToken ct)
        => Ok(await _service.ListarAccionesAsync(clave, ct));

    // GET api/cobranza/acciones/catalogo
    [HttpGet("acciones/catalogo")]
    public async Task<IActionResult> ObtenerCatalogoAcciones(CancellationToken ct)
        => Ok(await _service.ObtenerCatalogoAccionesAsync(ct));

    // GET api/cobranza/acciones/observaciones
    [HttpGet("acciones/observaciones")]
    public async Task<IActionResult> ObtenerCatalogoObservaciones(CancellationToken ct)
        => Ok(await _service.ObtenerCatalogoObservacionesAsync(ct));

    // GET api/cobranza/acciones/abogados
    [HttpGet("acciones/abogados")]
    public async Task<IActionResult> ObtenerAbogados(CancellationToken ct)
        => Ok(await _service.ObtenerAbogadosAsync(ct));

    // POST api/cobranza/acciones
    [HttpPost("acciones")]
    public async Task<IActionResult> RegistrarAccion(
        [FromBody] RegistrarAccionCobranzaRequest request, CancellationToken ct)
    {
        var ejecutadoPor = User.Identity?.Name ?? "sistema";
        var resultado = await _service.RegistrarAccionAsync(request, ejecutadoPor, ct);
        return Ok(resultado);
    }

    // GET api/cobranza/acciones/documentos/{documentoId}  → PDF snapshot archivado
    [HttpGet("acciones/documentos/{documentoId:int}")]
    public async Task<IActionResult> ObtenerDocumentoAccion(int documentoId, CancellationToken ct)
    {
        var doc = await _service.ObtenerDocumentoAccionAsync(documentoId, ct);
        if (doc is null)
            return NotFound();

        Response.Headers.ContentDisposition = $"inline; filename={doc.NombreArchivo}";
        return File(doc.Contenido, doc.ContentType);
    }

    // POST api/cobranza/acciones/{accionId}/documento  → regenera y archiva snapshot
    [HttpPost("acciones/{accionId:int}/documento")]
    public async Task<IActionResult> RegenerarDocumentoAccion(int accionId, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        var documentoId = await _service.RegenerarDocumentoAccionAsync(accionId, usuario, ct);
        return Ok(new { documentoId });
    }

    // GET api/cobranza/acciones/historial?desde=2026-06-01&hasta=2026-06-12&codAccion=&cliente=&ejecutadoPor=
    [HttpGet("acciones/historial")]
    public async Task<IActionResult> ListarHistorialAcciones(
        [FromQuery] DateTime desde, [FromQuery] DateTime hasta,
        [FromQuery] int? codAccion, [FromQuery] string? cliente,
        [FromQuery] string? ejecutadoPor, CancellationToken ct)
    {
        if (desde.Date > hasta.Date)
            return BadRequest("Rango de fechas inválido: 'desde' debe ser menor o igual a 'hasta'.");

        return Ok(await _service.ListarHistorialAccionesAsync(desde, hasta, codAccion, cliente, ejecutadoPor, ct));
    }

    // GET api/cobranza/bloqueo/{clave}
    [HttpGet("bloqueo/{clave}")]
    public async Task<IActionResult> ObtenerEstadoBloqueo(string clave, CancellationToken ct)
    {
        var estado = await _service.ObtenerEstadoBloqueoAsync(clave, ct);
        return estado is null ? NotFound() : Ok(estado);
    }

    // POST api/cobranza/bloqueo
    [HttpPost("bloqueo")]
    public async Task<IActionResult> BloquearDesbloquear(
        [FromBody] BloquearClienteRequest request, CancellationToken ct)
    {
        var username = User.Identity?.Name;
        if (string.IsNullOrWhiteSpace(username))
            return Unauthorized();

        // TODO: agregar verificación de password cuando se confirme el namespace de ApplicationUser
        var usuario = username;
        await _service.BloquearDesbloquearAsync(
            request.ClienteClave, request.Bloquear, request.Motivo, usuario, ct);

        return Ok(new { success = true, bloqueado = request.Bloquear });
    }

    // GET api/cobranza/clientes/{clave}/llamadas
    [HttpGet("clientes/{clave}/llamadas")]
    public async Task<IActionResult> ListarLlamadas(string clave, CancellationToken ct)
        => Ok(await _service.ListarLlamadasAsync(clave, ct));

    // POST api/cobranza/llamadas
    [HttpPost("llamadas")]
    public async Task<IActionResult> RegistrarLlamada(
        [FromBody] RegistrarLlamadaRequest request,
        CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        await _service.RegistrarLlamadaAsync(request, usuario, ct);
        return Ok();
    }

    // GET api/cobranza/clientes/{clave}/notas-cobro
    [HttpGet("clientes/{clave}/notas-cobro")]
    public async Task<IActionResult> ListarNotasCobro(string clave, CancellationToken ct)
        => Ok(await _service.ListarNotasCobroAsync(clave, ct));

    // POST api/cobranza/notas-cobro
    [HttpPost("notas-cobro")]
    public async Task<IActionResult> EmitirNotaCobro(
        [FromBody] EmitirNotaCobroRequest request, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        var nota = await _service.EmitirNotaCobroAsync(request, usuario, ct);
        return Ok(nota);
    }

    // PATCH api/cobranza/notas-cobro/{id}/anular
    [HttpPatch("notas-cobro/{id:int}/anular")]
    public async Task<IActionResult> AnularNotaCobro(
        int id, [FromBody] AnularNotaCobroRequest request, CancellationToken ct)
    {
        await _service.AnularNotaCobroAsync(id, request.Motivo, ct);
        return Ok();
    }

    // ── CRUD Catálogos ────────────────────────────────────────────────────────

    [HttpGet("catalogos/acciones")]
    public async Task<IActionResult> ListarAccionesCrud(CancellationToken ct)
        => Ok(await _service.ListarAccionesCrudAsync(ct));

    [HttpPost("catalogos/acciones")]
    public async Task<IActionResult> GuardarAccion([FromBody] AccionCobranzaSaveDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Nombre))
            return BadRequest("El nombre es requerido.");
        await _service.GuardarAccionAsync(dto, ct);
        return Ok();
    }

    [HttpGet("catalogos/observaciones")]
    public async Task<IActionResult> ListarObservacionesCrud(CancellationToken ct)
        => Ok(await _service.ListarObservacionesCrudAsync(ct));

    [HttpPost("catalogos/observaciones")]
    public async Task<IActionResult> GuardarObservacion([FromBody] ObservacionCobranzaSaveDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Observacion))
            return BadRequest("La observación es requerida.");
        await _service.GuardarObservacionAsync(dto, ct);
        return Ok();
    }

    // ── Clientes para cobros ──────────────────────────────────────────────────

    // GET api/Cobranza/clientes-cobro?...
    [HttpGet("clientes-cobro")]
    public async Task<IActionResult> ListarClientesCobro(
        [FromQuery] ClienteCobroFiltroDto filtro, CancellationToken ct)
        => Ok(await _service.ListarClientesCobroAsync(filtro, ct));

    // GET api/Cobranza/cartera-vencida?fechaCorte=&busqueda=&tramo=&cicloId=
    [HttpGet("cartera-vencida")]
    public async Task<IActionResult> ListarCarteraVencida(
        [FromQuery] CarteraVencidaFiltroDto filtro, CancellationToken ct)
        => Ok(await _service.ListarCarteraVencidaAsync(filtro, ct));

    // POST api/Cobranza/acciones/lote
    [HttpPost("acciones/lote")]
    public async Task<IActionResult> RegistrarAccionLote(
        [FromBody] RegistrarAccionLoteRequest request, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        var n = await _service.RegistrarAccionLoteAsync(request, usuario, ct);
        return Ok(new { registradas = n });
    }

    // POST api/Cobranza/cartas-cobro  → genera lote y devuelve encabezado
    [HttpPost("cartas-cobro")]
    public async Task<IActionResult> GenerarCartas(
        [FromBody] GenerarCartasCobroRequest request, CancellationToken ct)
    {
        var usuario = User.Identity?.Name ?? "sistema";
        var hdr = await _service.GenerarCartasCobroAsync(request, usuario, ct);
        return Ok(hdr);
    }

    // GET api/Cobranza/cartas-cobro/{id}/imprimir → HTML imprimible (una carta por página)
    [HttpGet("cartas-cobro/{id:int}/imprimir")]
    public async Task<IActionResult> ImprimirCartas(int id, CancellationToken ct)
    {
        var lote = await _service.ObtenerCartaLoteAsync(id, ct);
        if (lote is null) return NotFound();
        return Content(RenderCartasHtml(lote), "text/html", System.Text.Encoding.UTF8);
    }

    // GET api/Cobranza/cartas-cobro/{id}/pdf
    [HttpGet("cartas-cobro/{id:int}/pdf")]
    public async Task<IActionResult> DescargarCartasPdf(int id, CancellationToken ct)
    {
        var lote = await _service.ObtenerCartaLoteAsync(id, ct);
        if (lote is null) return NotFound();

        using var report = new Rpt_Dev_Carta_Cobro(lote);
        using var stream = new System.IO.MemoryStream();
        report.ExportToPdf(stream);

        var fileName = $"cartas-cobro-{SafeFileNamePart(lote.Encabezado.Correlativo)}.pdf";
        return File(stream.ToArray(), "application/pdf", fileName);
    }

    private static string SafeFileNamePart(string value)
    {
        var invalidChars = System.IO.Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(c => invalidChars.Contains(c) ? '_' : c));
    }
    private static string RenderCartasHtml(CartaCobroLoteDto lote)
    {
        var es  = System.Globalization.CultureInfo.GetCultureInfo("es-HN");
        Func<string?, string> enc = s => System.Net.WebUtility.HtmlEncode(s ?? string.Empty);
        Func<decimal, string> money = v => v.ToString("N2", es);
        var hoy = DateTime.Today;
        var emp = lote.Empresa;
        var empNombre = !string.IsNullOrWhiteSpace(emp.RazonSocial) ? emp.RazonSocial! : emp.NombreComercial;
        var empCorto  = emp.NombreComercial;
        var logoSrc = (!string.IsNullOrEmpty(emp.LogoBase64) && !string.IsNullOrEmpty(emp.LogoMime))
            ? $"data:{emp.LogoMime};base64,{emp.LogoBase64}"
            : null;
        var plazo = lote.Encabezado.PlazoHoras ?? 24;

        static int PeriodKey(string? p)
        {
            if (string.IsNullOrWhiteSpace(p)) return 0;
            var parts = p.Split('/');
            return (parts.Length == 2 && int.TryParse(parts[0], out var y) && int.TryParse(parts[1], out var m))
                ? y * 100 + m : 0;
        }

        var sb = new System.Text.StringBuilder();
        sb.Append("<!DOCTYPE html><html lang=\"es\"><head><meta charset=\"utf-8\"/>")
          .Append("<title>Requerimiento de pago en mora ").Append(enc(lote.Encabezado.Correlativo)).Append("</title>")
          .Append("<style>")
          .Append("@page{size:Letter;margin:0.5in}")
          .Append("body{font-family:'Times New Roman',serif;font-size:11pt;color:#000;margin:0}")
          .Append(".req{page-break-after:always}")
          .Append(".logo{display:block;margin:0 auto 4px;max-height:72px}")
          .Append(".empresa{text-align:center;font-weight:bold;font-size:15pt}")
          .Append(".subt{text-align:center;font-weight:bold;font-size:11pt;margin-top:2px}")
          .Append(".rule{border:0;border-top:3px solid #000;margin:8px 0}")
          .Append(".datos{width:100%;font-size:10pt}.datos td{vertical-align:top;padding:1px 0}")
          .Append(".linea{font-size:10pt;margin-top:2px}")
          .Append("table.saldos{width:100%;border-collapse:collapse;font-size:9.5pt;margin:10px 0;text-align:center}")
          .Append("table.saldos th,table.saldos td{padding:3px 6px}")
          .Append(".num{text-align:right}.totmora{text-align:right;font-weight:bold;font-style:italic;margin:6px 0}")
          .Append(".legal{font-size:10pt;margin:5px 0}.slogan{text-align:center;margin:14px 0}")
          .Append("table.firmas{margin-left:auto;font-size:10pt;margin-top:8px}")
          .Append("table.firmas td{padding:3px 6px}.fl{text-align:right;font-style:italic;font-weight:bold;white-space:nowrap}")
          .Append(".fln{border-bottom:1px solid #000;width:230px}")
          .Append(".unidad{font-style:italic;font-weight:bold;font-size:10pt;border-top:1px solid #000;width:200px;padding-top:2px;margin-top:14px}")
          .Append(".obs{font-size:10pt;margin-top:10px}.obs .l{display:inline-block;border-bottom:1px solid #000;width:80%}")
          .Append(".no-print{padding:10px}@media print{.no-print{display:none}}")
          .Append("</style></head><body>");

        sb.Append("<div class=\"no-print\"><button onclick=\"window.print()\">Imprimir / Guardar PDF</button></div>");

        foreach (var c in lote.Clientes)
        {
            var movs = c.Detalle.Where(d => d.SaldoDetalle != 0).ToList();
            var maxPeriod = movs.Count > 0 ? movs.Max(d => PeriodKey(d.Periodo)) : 0;
            var grupos = movs
                .GroupBy(d => string.IsNullOrWhiteSpace(d.TipoServicio)
                    ? (string.IsNullOrWhiteSpace(d.Descripcion) ? "Servicio" : d.Descripcion)
                    : d.TipoServicio!)
                .Select(g => new
                {
                    Servicio = g.Key,
                    Actual   = g.Where(d => PeriodKey(d.Periodo) == maxPeriod).Sum(d => d.SaldoDetalle),
                    Anterior = g.Where(d => PeriodKey(d.Periodo) != maxPeriod).Sum(d => d.SaldoDetalle)
                })
                .OrderBy(x => x.Servicio)
                .ToList();

            sb.Append("<div class=\"req\">");

            if (logoSrc is not null)
                sb.Append("<img class=\"logo\" src=\"").Append(logoSrc).Append("\" alt=\"logo\"/>");
            sb.Append("<div class=\"empresa\">").Append(enc(empNombre)).Append("</div>");
            sb.Append("<div class=\"subt\">REQUERIMIENTO DE PAGO EN MORA&nbsp;&nbsp;#&nbsp;").Append(Math.Max(c.NumeroRequerimiento, 1)).Append("</div>");
            sb.Append("<hr class=\"rule\"/>");

            sb.Append("<table class=\"datos\"><tr><td>")
              .Append("<div><b>Clave :</b> ").Append(enc(c.Clave)).Append("</div>")
              .Append("<div><b>Propietario :</b> ").Append(enc(c.Nombre)).Append("</div>")
              .Append("<div><b>Direccion :</b> ").Append(enc(c.Direccion)).Append("</div>")
              .Append("</td><td style=\"text-align:right\">")
              .Append("<div><b>Fecha Emisión</b> ").Append(hoy.ToString("dd/MM/yy", es)).Append("</div>")
              .Append("<div><b>No.Identidad :</b> ").Append(enc(c.Identidad)).Append("</div>")
              .Append("</td></tr></table>");
            sb.Append("<div class=\"linea\"><b>Medidor :</b> ").Append(enc(c.Medidor))
              .Append(" &nbsp;&nbsp; <b>Ciclo :</b> ").Append(enc(c.CicloId?.ToString()))
              .Append(" &nbsp;&nbsp; <b>Libreta :</b> ").Append(enc(c.Libreta))
              .Append(" &nbsp;&nbsp; <b>Secuencia :</b> ").Append(enc(c.Secuencia)).Append("</div>");

            sb.Append("<p>Estimado Usuario :</p>");
            sb.Append("<p>Hacemos de su conocimiento que a la fecha tiene una mora Pendiente de LPS.")
              .Append(money(c.SaldoTotal)).Append("</p>");

            sb.Append("<table class=\"saldos\"><thead>")
              .Append("<tr><th rowspan=\"2\" style=\"text-align:left\">DESCRIPCION</th>")
              .Append("<th colspan=\"2\">SALDOS ANTERIORES</th><th colspan=\"2\">SALDOS MES ACTUAL</th>")
              .Append("<th rowspan=\"2\">TOTAL</th></tr>")
              .Append("<tr><th>SALDOS</th><th>RECARGOS</th><th>SALDOS</th><th>RECARGOS</th></tr></thead><tbody>");
            foreach (var g in grupos)
                sb.Append("<tr><td style=\"text-align:left\">").Append(enc(g.Servicio)).Append("</td>")
                  .Append("<td class=\"num\">").Append(money(g.Anterior)).Append("</td><td class=\"num\">0.00</td>")
                  .Append("<td class=\"num\">").Append(money(g.Actual)).Append("</td><td class=\"num\">0.00</td>")
                  .Append("<td class=\"num\">").Append(money(g.Anterior + g.Actual)).Append("</td></tr>");
            sb.Append("</tbody></table>");

            sb.Append("<div class=\"totmora\">TOTAL mora: &nbsp;&nbsp; ").Append(money(c.SaldoTotal)).Append("</div>");

            sb.Append("<p class=\"legal\">Por antes expuesto se le brinda un plazo de <b>").Append(plazo)
              .Append(" HORAS</b> para realizar un plan de pago, lo cual debera presentarse a las oficinas de Atencion al Cliente; DEPARTAMENTO DE COBRANZAS.</p>");
            sb.Append("<p class=\"legal\">En caso contrario ").Append(enc(empCorto))
              .Append(", da por terminado el plazo que se le concedio y procede a la recuperacion total de su obligacion a traves de la via JUDICIAL, por medio de nuestro apoderado legal.</p>");
            sb.Append("<p class=\"legal\">Asi mismo hacemos de su conocimiento que al ser trasladado a esta instancia incurre en un cargo del 25% por concepto de honorarios de abogado por el valor en mora. Esperando una pronta respuesta a este llamado.</p>");

            sb.Append("<div class=\"slogan\">NO SE APRECIA EL VALOR DEL AGUA HASTA QUE SE SECA EL POZO</div>");

            sb.Append("<table class=\"firmas\">")
              .Append("<tr><td class=\"fl\">Recibida por:</td><td class=\"fln\"></td></tr>")
              .Append("<tr><td class=\"fl\">Identidad:</td><td class=\"fln\"></td></tr>")
              .Append("<tr><td class=\"fl\">Telefono:</td><td class=\"fln\"></td></tr>")
              .Append("<tr><td class=\"fl\">Fecha Recibido:</td><td class=\"fln\"></td></tr></table>");

            sb.Append("<div class=\"unidad\">Unidad de Cobranzas A.P.C.</div>");
            sb.Append("<div class=\"obs\"><b>Observacion:</b> <span class=\"l\"></span></div>");

            sb.Append("</div>");
        }

        sb.Append("</body></html>");
        return sb.ToString();
    }

}



