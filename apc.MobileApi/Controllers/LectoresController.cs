using apc.MobileApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.MobileApi;
using SIAD.Services.MobileApi;

namespace apc.MobileApi.Controllers;

/// <summary>
/// Login / logout / perfil del lector. El login valida código+clave (bcrypt) y
/// emite un token bearer; el resto del árbol /api exige ese token.
/// </summary>
[ApiController]
[Route("api/lectores")]
public sealed class LectoresController : ControllerBase
{
    private readonly ILectoresMobileService _service;
    private readonly MobileApiRequestContext _requestContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LectoresController> _logger;

    public LectoresController(
        ILectoresMobileService service,
        MobileApiRequestContext requestContext,
        IConfiguration configuration,
        ILogger<LectoresController> logger)
    {
        _service = service;
        _requestContext = requestContext;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>Login del lector. Devuelve token bearer + perfil, o 401 si la credencial es inválida.</summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Clave))
        {
            return BadRequest(new { codigo = "CREDENCIAL_INCOMPLETA", mensaje = "Código y clave son obligatorios." });
        }

        var horas = _configuration.GetValue("MobileApi:SesionHoras", 12);
        try
        {
            var respuesta = await _service.LoginAsync(request, TimeSpan.FromHours(horas), ct);
            if (respuesta is null)
            {
                return Unauthorized(new { codigo = "CREDENCIAL_INVALIDA", mensaje = "Código o clave incorrectos." });
            }

            return Ok(respuesta);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login de lector falló para código {Codigo}.", request.Codigo);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { codigo = "ERROR_LOGIN", mensaje = "No se pudo procesar el login." });
        }
    }

    /// <summary>Cierra la sesión del token actual.</summary>
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var token = BearerToken.Parse(Request);
        await _service.LogoutAsync(token, ct);
        return NoContent();
    }

    /// <summary>Perfil del lector autenticado.</summary>
    [HttpGet("perfil")]
    public IActionResult Perfil()
    {
        var sesion = _requestContext.Sesion;
        if (sesion is null)
        {
            return Unauthorized(new { codigo = "NO_AUTORIZADO", mensaje = "Sesión no válida." });
        }

        return Ok(new LectorPerfilDto
        {
            Codigo = sesion.Codigo,
            Nombre = sesion.Nombre,
            Ruta = sesion.Ruta,
            CodCiclo = sesion.CodCiclo,
        });
    }
}
