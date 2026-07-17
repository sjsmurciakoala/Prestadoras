using apc.MobileApi.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.MobileApi;
using SIAD.Services.MobileApi;

namespace apc.MobileApi.Controllers;

/// <summary>
/// Subida de lectura V3 (paridad ActualizarLecturaV3). Idempotente por UUID;
/// los conflictos de sincronización devuelven 409. El tenant sale de la sesión
/// (A6), nunca del payload.
/// </summary>
[ApiController]
[Route("api/lecturas")]
public sealed class LecturasController : ControllerBase
{
    private readonly ILectoresMobileService _service;
    private readonly MobileApiRequestContext _requestContext;
    private readonly ILogger<LecturasController> _logger;

    public LecturasController(
        ILectoresMobileService service,
        MobileApiRequestContext requestContext,
        ILogger<LecturasController> logger)
    {
        _service = service;
        _requestContext = requestContext;
        _logger = logger;
    }

    /// <summary>Registra la lectura del medidor y emite la factura V3.</summary>
    [HttpPost]
    public async Task<IActionResult> Actualizar([FromBody] LecturaV3Request request, CancellationToken ct)
    {
        if (request is null)
        {
            return BadRequest(new { codigo = "REQUEST_INVALIDO", mensaje = "El cuerpo de la lectura es obligatorio." });
        }

        // Si el payload no trae usuario, la factura quedaría firmada como "mobileapi"
        // (el fallback del servicio). El lector autenticado de la sesión es la
        // identidad real — igual que el tenant, no depende del payload (A6).
        if (string.IsNullOrWhiteSpace(request.Usuario))
        {
            request.Usuario = _requestContext.Sesion?.Codigo;
        }

        try
        {
            var respuesta = await _service.ActualizarLecturaAsync(request, _requestContext.CompanyId, ct);
            return respuesta.Codigo switch
            {
                "TENANT_MISMATCH" => StatusCode(StatusCodes.Status403Forbidden, respuesta),
                "CLIENTE_NO_ENCONTRADO" => NotFound(respuesta),
                "CLAVE_REQUERIDA" or "CAI_FORMAL_REQUERIDO" => BadRequest(respuesta),
                "SYNC_CONFLICT_TOTAL" or "FACTURA_YA_EMITIDA" => Conflict(respuesta),
                _ => Ok(respuesta),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ActualizarLecturaV3 falló para clave {Clave}.", request.Clave);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new LecturaV3Respuesta { Success = false, Codigo = "ERROR_LECTURA_V3", Mensaje = "No se pudo registrar la lectura." });
        }
    }
}
