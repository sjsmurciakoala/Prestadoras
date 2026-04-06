using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;
using SIAD.Core.Tenancy;
using apc.Security;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/polizas")]
[ModuleAuthorize(PermissionModules.Contabilidad)]
public sealed class PolizasController : ControllerBase
{
    private readonly IPolizaService _polizas;
    private readonly ICurrentCompanyService _currentCompany;

    public PolizasController(IPolizaService polizas, ICurrentCompanyService currentCompany)
    {
        _polizas = polizas;
        _currentCompany = currentCompany;
    }

    [HttpGet("{id:long}")]
    public async Task<IActionResult> Obtener(long id, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var dto = await _polizas.ObtenerAsync(companyId, id, ct);
        return Ok(dto);
    }

    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] long? periodId, [FromQuery] long? journalId, [FromQuery] int skip = 0, [FromQuery] int take = 100, CancellationToken ct = default)
    {
        var companyId = _currentCompany.GetCompanyId();
        if (periodId.HasValue)
        {
            var list = await _polizas.ListarPorPeriodoAsync(companyId, periodId.Value, skip, take, ct);
            return Ok(list);
        }
        if (journalId.HasValue)
        {
            var list = await _polizas.ListarPorDiarioAsync(companyId, journalId.Value, skip, take, ct);
            return Ok(list);
        }
        return BadRequest(new ProblemDetails { Title = "Parámetros insuficientes", Detail = "Debe especificar periodId o journalId" });
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] PolizaCrearRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var companyId = _currentCompany.GetCompanyId();
        var userId = User?.Identity?.Name ?? "SYSTEM";

        // Convertir fecha a UTC
        var polizaDate = req.PolizaDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(req.PolizaDate, DateTimeKind.Utc)
            : req.PolizaDate.ToUniversalTime();

        var lineas = req.Lineas?.Select(l => new PolizaLineaCrearDto(
            (short)0,
            l.AccountId,
            l.CostCenterId,
            l.ThirdPartyId,
            l.DebitAmount,
            l.CreditAmount,
            l.CurrencyCode,
            l.ExchangeRate,
            l.Description,
            l.SourceDocument
        )).ToList() ?? new List<PolizaLineaCrearDto>();

        var id = await _polizas.CrearAsync(
            companyId,
            req.TypeId,
            req.PeriodId,
            req.JournalId,
            polizaDate,
            req.Module,
            req.DocumentType,
            req.DocumentId,
            req.DocumentNumber,
            req.Description ?? string.Empty,
            lineas,
            userId,
            ct);
        return Created($"api/contabilidad/polizas/{id}", new { id });
    }

    [HttpPut("{id:long}")]
    public async Task<IActionResult> Actualizar(long id, [FromBody] PolizaActualizarRequest req, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var userId = User?.Identity?.Name ?? "SYSTEM";
        await _polizas.ActualizarAsync(companyId, id, new PolizaActualizarDto(req.PolizaDate, req.Description), userId, ct);
        return NoContent();
    }

    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Eliminar(long id, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        await _polizas.EliminarAsync(companyId, id, ct);
        return NoContent();
    }

    [HttpPost("{id:long}/registrar")]
    public async Task<IActionResult> Registrar(long id, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var userId = User?.Identity?.Name ?? "SYSTEM";
        await _polizas.RegistrarAsync(companyId, id, userId, ct);
        return Ok(new { id, status = "POSTED" });
    }

    [HttpPost("{id:long}/revertir")]
    public async Task<IActionResult> Revertir(long id, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        var userId = User?.Identity?.Name ?? "SYSTEM";
        await _polizas.RevertirAsync(companyId, id, userId, ct);
        return Ok(new { id, status = "DRAFT" });
    }

    [HttpGet("{id:long}/validar")]
    public async Task<IActionResult> Validar(long id, CancellationToken ct)
    {
        var companyId = _currentCompany.GetCompanyId();
        (bool ok, decimal debit, decimal credit) = await _polizas.ValidarBalanceAsync(companyId, id, ct);
        return Ok(new { balanceado = ok, totalDebito = debit, totalCredito = credit });
    }
}

