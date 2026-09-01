using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Services.Proveedores;
using apc.Security;

namespace apc.Controllers.Proveedores;

/// <summary>
/// Scorecard de proveedores: períodos, ranking, ficha y captura de los criterios manuales.
/// <para>
/// Módulo de permisos: <c>proveedores</c>, recurso FINO <c>evaluacion</c>
/// (<see cref="PermissionResources.Proveedores.Evaluacion"/>). El método HTTP decide la acción:
/// GET→View (consultar) y POST/PUT→Create/Edit (calcular, calificar, cerrar). La empresa la
/// resuelve el tenant; ningún parámetro de la ruta la decide.
/// </para>
/// </summary>
[ApiController]
[Route("api/proveedores/evaluacion")]
[ModuleAuthorize(PermissionModules.Proveedores, PermissionResources.Proveedores.Evaluacion)]
public sealed class EvaluacionProveedorController : ControllerBase
{
    private readonly IEvaluacionProveedorService _service;

    public EvaluacionProveedorController(IEvaluacionProveedorService service) => _service = service;

    /// <summary>Períodos de evaluación de la empresa, del más reciente al más viejo.</summary>
    [HttpGet("periodos")]
    public async Task<IActionResult> GetPeriodos(CancellationToken ct)
        => Ok(await _service.GetPeriodosAsync(ct));

    [HttpGet("periodos/{periodoId:int}")]
    public async Task<IActionResult> GetPeriodo(int periodoId, CancellationToken ct)
    {
        var periodo = await _service.GetPeriodoAsync(periodoId, ct);
        return periodo is null
            ? NotFound(new { mensaje = "El período de evaluación no existe." })
            : Ok(periodo);
    }

    /// <summary>Abre un período nuevo (rango de fechas con nombre).</summary>
    [HttpPost("periodos")]
    public async Task<IActionResult> CrearPeriodo(
        [FromBody] EvaluacionPeriodoUpsertDto dto, CancellationToken ct)
    {
        try
        {
            var creado = await _service.CrearPeriodoAsync(dto, Usuario(), ct);
            return CreatedAtAction(nameof(GetPeriodo), new { periodoId = creado.Id }, creado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    /// <summary>
    /// Recalcula todas las evaluaciones del período. Respeta lo capturado a mano; falla si el
    /// período ya está cerrado.
    /// </summary>
    [HttpPost("periodos/{periodoId:int}/calcular")]
    public async Task<IActionResult> Calcular(int periodoId, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.CalcularAsync(periodoId, Usuario(), ct));
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

    /// <summary>Congela el período: deja de recalcularse y de admitir capturas.</summary>
    [HttpPost("periodos/{periodoId:int}/cerrar")]
    public async Task<IActionResult> Cerrar(int periodoId, CancellationToken ct)
    {
        try
        {
            return await _service.CerrarPeriodoAsync(periodoId, Usuario(), ct)
                ? NoContent()
                : NotFound(new { mensaje = "El período de evaluación no existe." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    /// <summary>Ranking del período con el desglose por criterio de cada proveedor.</summary>
    [HttpGet("periodos/{periodoId:int}/ranking")]
    public async Task<IActionResult> GetRanking(
        int periodoId,
        [FromQuery] string? search,
        [FromQuery] string? clase,
        [FromQuery] decimal? comprasMinimas,
        CancellationToken ct)
    {
        var filtro = new EvaluacionFilterDto
        {
            Search = search,
            ClaseCodigo = clase,
            ComprasMinimas = comprasMinimas
        };

        return Ok(await _service.GetRankingAsync(periodoId, filtro, ct));
    }

    /// <summary>Ficha de un proveedor en el período: evidencia por criterio e historial.</summary>
    [HttpGet("periodos/{periodoId:int}/proveedores/{codigo}")]
    public async Task<IActionResult> GetFicha(int periodoId, string codigo, CancellationToken ct)
    {
        var ficha = await _service.GetFichaAsync(periodoId, codigo, ct);
        return ficha is null
            ? NotFound(new { mensaje = "El proveedor no tiene evaluación en este período." })
            : Ok(ficha);
    }

    /// <summary>Califica un criterio manual y/o guarda el plan de acción.</summary>
    [HttpPut("periodos/{periodoId:int}/proveedores/{codigo}")]
    public async Task<IActionResult> Capturar(
        int periodoId, string codigo, [FromBody] EvaluacionCapturaDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.CapturarAsync(periodoId, codigo, dto, Usuario(), ct));
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

    /// <summary>
    /// PDF de la ficha de evaluación del proveedor (inline). Sólo lee: es reimprimible.
    /// </summary>
    [HttpGet("periodos/{periodoId:int}/proveedores/{codigo}/pdf")]
    public async Task<IActionResult> GetFichaPdf(int periodoId, string codigo, CancellationToken ct)
    {
        var datos = await _service.GetDatosFichaImpresionAsync(periodoId, codigo, User?.Identity?.Name, ct);
        if (datos is null)
        {
            return NotFound(new { mensaje = "El proveedor no tiene evaluación en este período." });
        }

        using var report = new SIAD.Reports.Rpt_Dev_Evaluacion_Proveedor(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition =
            $"inline; filename=evaluacion_{datos.Ficha.CodProveedor}_{datos.Ficha.PeriodoCodigo}_{stamp}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }

    /// <summary>PDF del cuadro comparativo del período (inline), con los mismos filtros del ranking.</summary>
    [HttpGet("periodos/{periodoId:int}/pdf")]
    public async Task<IActionResult> GetComparativoPdf(
        int periodoId,
        [FromQuery] string? search,
        [FromQuery] string? clase,
        [FromQuery] decimal? comprasMinimas,
        CancellationToken ct = default)
    {
        var filtro = new EvaluacionFilterDto
        {
            Search = search,
            ClaseCodigo = clase,
            ComprasMinimas = comprasMinimas
        };

        var datos = await _service.GetDatosComparativoImpresionAsync(
            periodoId, filtro, User?.Identity?.Name, ct);

        if (datos is null)
        {
            return NotFound(new { mensaje = "El período de evaluación no existe." });
        }

        using var report = new SIAD.Reports.Rpt_Dev_Evaluacion_Comparativo(datos);
        using var ms = new MemoryStream();
        report.ExportToPdf(ms);

        var stamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        Response.Headers.ContentDisposition =
            $"inline; filename=evaluacion_proveedores_{datos.PeriodoCodigo}_{stamp}.pdf";
        return File(ms.ToArray(), "application/pdf");
    }

    /// <summary>Criterios ACTIVOS (los que usa el cálculo y las columnas del ranking).</summary>
    [HttpGet("criterios")]
    public async Task<IActionResult> GetCriterios(CancellationToken ct)
        => Ok(await _service.GetCriteriosAsync(ct));

    /// <summary>Catálogo completo, incluidos los inactivos: es lo que edita el mantenimiento.</summary>
    [HttpGet("criterios/catalogo")]
    public async Task<IActionResult> GetCriteriosCatalogo(CancellationToken ct)
        => Ok(await _service.GetCriteriosCatalogoAsync(ct));

    [HttpPost("criterios")]
    public async Task<IActionResult> CrearCriterio(
        [FromBody] EvaluacionCriterioUpsertDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.CrearCriterioAsync(dto, Usuario(), ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("criterios/{id:int}")]
    public async Task<IActionResult> ActualizarCriterio(
        int id, [FromBody] EvaluacionCriterioUpsertDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ActualizarCriterioAsync(id, dto, Usuario(), ct));
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

    [HttpDelete("criterios/{id:int}")]
    public async Task<IActionResult> EliminarCriterio(int id, CancellationToken ct)
    {
        try
        {
            return await _service.EliminarCriterioAsync(id, ct)
                ? NoContent()
                : NotFound(new { mensaje = "El criterio no existe." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    /// <summary>Escala de clases (A/B/C/D) con sus rangos.</summary>
    [HttpGet("clases")]
    public async Task<IActionResult> GetClases(CancellationToken ct)
        => Ok(await _service.GetClasesAsync(ct));

    [HttpPost("clases")]
    public async Task<IActionResult> CrearClase(
        [FromBody] EvaluacionClaseUpsertDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.CrearClaseAsync(dto, ct));
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    [HttpPut("clases/{id:int}")]
    public async Task<IActionResult> ActualizarClase(
        int id, [FromBody] EvaluacionClaseUpsertDto dto, CancellationToken ct)
    {
        try
        {
            return Ok(await _service.ActualizarClaseAsync(id, dto, ct));
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

    [HttpDelete("clases/{id:int}")]
    public async Task<IActionResult> EliminarClase(int id, CancellationToken ct)
    {
        try
        {
            return await _service.EliminarClaseAsync(id, ct)
                ? NoContent()
                : NotFound(new { mensaje = "La clase no existe." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { mensaje = ex.Message });
        }
    }

    private string Usuario() => User?.Identity?.Name ?? "sistema";
}
