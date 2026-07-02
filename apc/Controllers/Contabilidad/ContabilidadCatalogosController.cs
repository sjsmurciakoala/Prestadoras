using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;
using Microsoft.AspNetCore.Hosting;
using apc.Security;
namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad/catalogos")]
[ModuleAuthorize(PermissionModules.Contabilidad)]
public class ContabilidadCatalogosController : ControllerBase
{
    private readonly IContabilidadCatalogosService _catalogosService;
    private readonly IWebHostEnvironment _env;
    public ContabilidadCatalogosController(IContabilidadCatalogosService catalogosService, IWebHostEnvironment env)
    {
        _catalogosService = catalogosService;
        _env = env;
    }

    [HttpGet("plan-cuentas")]
    public async Task<IActionResult> GetPlanCuentas(CancellationToken cancellationToken)
    {
        try
        {
            var cuentas = await _catalogosService.GetPlanCuentasAsync(cancellationToken);
            return Ok(cuentas);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpPost("plan-cuentas")]
    public async Task<IActionResult> SavePlanCuenta([FromBody] PlanCuentaUpsertDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userName = User?.Identity?.Name ?? "system";
            var resultId = await _catalogosService.SavePlanCuentaAsync(request with { User = userName }, cancellationToken);
            return Ok(new { id = resultId });
        }
        catch (DbUpdateException ex) when (IsDuplicatePlanCuentaCode(ex))
        {
            return Conflict("Ya existe una cuenta con este codigo.");
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Ya existe una cuenta", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("plan-cuentas/paged")]
    public async Task<IActionResult> GetPlanCuentasPaged(
        [FromQuery] PlanCuentaCatalogoFilterDto filtro,
        [FromQuery] int skip,
        [FromQuery] int take,
        [FromQuery] string? sortField,
        [FromQuery] bool sortDesc,
        CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var result = await _catalogosService.GetPlanCuentasPagedAsync(filtro, skip, take, sortField, sortDesc, cancellationToken);
            return Ok(result);
        });
    }

    [HttpGet("plan-cuentas/buscar")]
    public async Task<IActionResult> BuscarPlanCuenta([FromQuery] string cuenta, CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var result = await _catalogosService.BuscarPlanCuentaAsync(cuenta, cancellationToken);
            return Ok(result);
        });
    }

    [HttpGet("centros-costo")]
    public async Task<IActionResult> GetCentrosCosto(CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var centros = await _catalogosService.GetCentrosCostoAsync(cancellationToken);
            return Ok(centros);
        });
    }

    [HttpGet("tipos-partida")]
    public async Task<IActionResult> GetTiposPartida(CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var tipos = await _catalogosService.GetTiposPartidaAsync(cancellationToken);
            return Ok(tipos);
        });
    }

    [HttpPost("centros-costo")]
    public async Task<IActionResult> SaveCentroCosto([FromBody] CentroCostoUpsertDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userName = User?.Identity?.Name ?? "system";
            var resultId = await _catalogosService.SaveCentroCostoAsync(request with { User = userName }, cancellationToken);
            return Ok(new { id = resultId });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("Ya existe", StringComparison.OrdinalIgnoreCase))
        {
            return Conflict(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("centros-costo/{costCenterId:long}")]
    public async Task<IActionResult> DeleteCentroCosto(long costCenterId, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _catalogosService.DeleteCentroCostoAsync(costCenterId, cancellationToken);
            return deleted ? Ok() : NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("diarios")]
    public async Task<IActionResult> GetDiarios(CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var diarios = await _catalogosService.GetDiariosAsync(cancellationToken);
            return Ok(diarios);
        });
    }

    [HttpPost("diarios")]
    public async Task<IActionResult> SaveDiario([FromBody] DiarioUpsertDto request, CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var userName = User?.Identity?.Name ?? "system";
            var resultId = await _catalogosService.SaveDiarioAsync(request with { User = userName }, cancellationToken);
            return Ok(new { id = resultId });
        });
    }

    [HttpGet("periodos")]
    public async Task<IActionResult> GetPeriodos(CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var periodos = await _catalogosService.GetPeriodosAsync(cancellationToken);
            return Ok(periodos);
        });
    }

    [HttpPost("periodos")]
    public async Task<IActionResult> SavePeriodo([FromBody] PeriodoContableUpsertDto request, CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var userName = User?.Identity?.Name ?? "system";
            var resultId = await _catalogosService.SavePeriodoAsync(request with { User = userName }, cancellationToken);
            return Ok(new { id = resultId });
        });
    }

    [HttpPost("periodos/{periodId:long}/cerrar")]
    public async Task<IActionResult> ClosePeriodo(long periodId, CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var userName = User?.Identity?.Name ?? "system";
            var closed = await _catalogosService.ClosePeriodoAsync(periodId, userName, cancellationToken);
            return closed ? Ok() : NotFound();
        });
    }

    [HttpGet("plan-cuentas/plantilla")]
    public IActionResult DownloadPlanCuentasTemplate()
    {
        var path = Path.Combine(_env.WebRootPath, "templates", "PlanCuentasTemplate.xlsx");
        return PhysicalFile(
            path,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "PlanCuentasTemplate.xlsx");


    }
    [HttpPost("plan-cuentas/import")]
    public async Task<IActionResult> ImportPlanCuentas(
        [FromForm] IFormFile file,
        [FromForm] bool dryRun,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
        {
            return BadRequest("Archivo Requerido");

        }
        var userName = User?.Identity?.Name ?? "system";
        try
        {
            await using var stream = file.OpenReadStream();
            var result = await _catalogosService.ImportPlanCuentasAsync(stream, userName, dryRun, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    private static bool IsDuplicatePlanCuentaCode(DbUpdateException ex)
    {
        if (ex.InnerException is not PostgresException pg)
        {
            return false;
        }

        return pg.SqlState == "23505" &&
               string.Equals(pg.ConstraintName, "IX_con_plan_cuentas_company_id_code", StringComparison.OrdinalIgnoreCase);
    }
    [HttpGet("tipos-transaccion")]
public async Task<IActionResult> GetTiposTransaccion(CancellationToken cancellationToken)
{
    return await ExecuteCatalogActionAsync(async () =>
    {
        var items = await _catalogosService.GetTiposTransaccionAsync(cancellationToken);
        return Ok(items);
    });
}

[HttpPost("tipos-transaccion")]
public async Task<IActionResult> SaveTipoTransaccion([FromBody] TipoTransaccionUpsertDto request, CancellationToken cancellationToken)
{
    try
    {
        var userName = User?.Identity?.Name ?? "system";
        var id = await _catalogosService.SaveTipoTransaccionAsync(request with { User = userName }, cancellationToken);
        return Ok(new { id });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
}

[HttpDelete("tipos-transaccion/{typeId:long}")]
public async Task<IActionResult> DeleteTipoTransaccion(long typeId, CancellationToken cancellationToken)
{
    try
    {
        var deleted = await _catalogosService.DeleteTipoTransaccionAsync(typeId, cancellationToken);
        return deleted ? Ok() : NotFound();
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
}

[HttpGet("tipos-transaccion/{typeId:long}/rules")]
public async Task<IActionResult> GetTipoTransaccionRules(long typeId, CancellationToken cancellationToken)
{
    return await ExecuteCatalogActionAsync(async () =>
    {
        var rules = await _catalogosService.GetTipoTransaccionRulesAsync(typeId, cancellationToken);
        return Ok(rules);
    });
}

[HttpPost("tipos-transaccion/{typeId:long}/rules")]
public async Task<IActionResult> SaveTipoTransaccionRule(long typeId, [FromBody] TipoTransaccionRuleUpsertDto request, CancellationToken cancellationToken)
{
    try
    {
        var userName = User?.Identity?.Name ?? "system";
        var id = await _catalogosService.SaveTipoTransaccionRuleAsync(request with { TypeId = typeId, User = userName }, cancellationToken);
        return Ok(new { id });
    }
    catch (ArgumentException ex)
    {
        return BadRequest(ex.Message);
    }
    catch (InvalidOperationException ex)
    {
        return BadRequest(ex.Message);
    }
}

[HttpDelete("tipos-transaccion/rules/{ruleId:long}")]
  public async Task<IActionResult> DeleteTipoTransaccionRule(long ruleId, CancellationToken cancellationToken)
  {
      return await ExecuteCatalogActionAsync(async () =>
      {
          var deleted = await _catalogosService.DeleteTipoTransaccionRuleAsync(ruleId, cancellationToken);
          return deleted ? Ok() : NotFound();
      });
  }

    [HttpGet("reglas-abonos")]
    public async Task<IActionResult> GetReglasAbonos(CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var reglas = await _catalogosService.GetReglasIntegracionAbonosAsync(cancellationToken);
            return Ok(reglas);
        });
    }

    [HttpPost("reglas-abonos")]
    public async Task<IActionResult> SaveReglaAbono([FromBody] ReglaIntegracionUpsertDto request, CancellationToken cancellationToken)
    {
        try
        {
            var userName = User?.Identity?.Name ?? "system";
            var id = await _catalogosService.SaveReglaIntegracionAbonosAsync(request with { User = userName }, cancellationToken);
            return Ok(new { id });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpDelete("reglas-abonos/{reglaId:long}")]
    public async Task<IActionResult> DeleteReglaAbono(long reglaId, CancellationToken cancellationToken)
    {
        return await ExecuteCatalogActionAsync(async () =>
        {
            var deleted = await _catalogosService.DeleteReglaIntegracionAbonosAsync(reglaId, cancellationToken);
            return deleted ? Ok() : NotFound();
        });
    }

    private static async Task<IActionResult> ExecuteCatalogActionAsync(Func<Task<IActionResult>> action)
    {
        try
        {
            return await action();
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (ArgumentException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
        catch (InvalidOperationException ex)
        {
            return new BadRequestObjectResult(ex.Message);
        }
    }

}

