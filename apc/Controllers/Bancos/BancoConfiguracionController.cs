using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class BancoConfiguracionController : ControllerBase
{
    private readonly ICompanyAccessValidator accessValidator;
    private readonly IBancoConfiguracionService configuracionService;

    public BancoConfiguracionController(
        ICompanyAccessValidator accessValidator,
        IBancoConfiguracionService configuracionService)
    {
        this.accessValidator = accessValidator;
        this.configuracionService = configuracionService;
    }

    [HttpGet("configuracion/{companyId:long}")]
    public async Task<IActionResult> Obtener(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var config = await configuracionService.ObtenerAsync(companyId, ct);
            return Ok(config);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener la configuracion: {ex.Message}" });
        }
    }

    [HttpGet("configuracion/{companyId:long}/cuentas-mayores")]
    public async Task<IActionResult> ListarCuentasMayores(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var cuentas = await configuracionService.ListarCuentasMayoresAsync(companyId, ct);
            return Ok(cuentas);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener las cuentas mayores: {ex.Message}" });
        }
    }

    [HttpPost("configuracion/{companyId:long}")]
    public async Task<IActionResult> Guardar(long companyId, [FromBody] BancoConfiguracionDto dto, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await configuracionService.GuardarAsync(companyId, dto, usuario, ct);
            return Ok(resultado);
        }
        catch (DbUpdateException ex)
        {
            var raiz = ex.GetBaseException()?.Message ?? ex.Message;
            return BadRequest(new { detail = $"Error al guardar la configuracion: {raiz}" });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar la configuracion: {ex.Message}" });
        }
    }
}

