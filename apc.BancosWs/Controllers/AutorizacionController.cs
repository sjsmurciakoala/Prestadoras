using apc.BancosWs.Contrato;
using Microsoft.AspNetCore.Mvc;
using SIAD.Services.BancosWs;

namespace apc.BancosWs.Controllers;

/// <summary>
/// Réplica de SrvAutorizacion (contrato §2.2): validar llave, vigencia
/// informativa y genkey. Las respuestas de texto son literales del contrato.
/// </summary>
[ApiController]
[Route("simafi/api/auth")]
public sealed class AutorizacionController : ControllerBase
{
    private readonly IBancosWsService _service;

    public AutorizacionController(IBancosWsService service)
    {
        _service = service;
    }

    /// <summary>GET /auth/?key=&amp;banco= → 200 sin cuerpo, o 400 &lt;mensaje&gt; No autorizado.</summary>
    [HttpGet("")]
    public async Task<IActionResult> ValidarLlave([FromQuery] string? key, [FromQuery] string? banco, CancellationToken ct)
    {
        var credencial = await _service.AutenticarAsync(banco, key, ct);
        if (credencial is null)
        {
            return Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgNoAutorizado));
        }

        return StatusCode(StatusCodes.Status200OK);
    }

    /// <summary>GET /auth/vigencia/?banco= → "Vigencia:&lt;fecha&gt;" | "Vigencia: permanente" | 400.</summary>
    [HttpGet("vigencia")]
    public async Task<IActionResult> Vigencia([FromQuery] string? banco, CancellationToken ct)
    {
        var (existe, vigencia) = await _service.ObtenerVigenciaAsync(banco, ct);
        if (!existe)
        {
            return Texto(400, ContractXml.MsgNoExisteRegistroBanco);
        }

        return Texto(200, vigencia.HasValue ? $"Vigencia:{vigencia.Value:yyyy-MM-dd}" : "Vigencia: permanente");
    }

    /// <summary>GET /auth/genkey/?banco=&amp;vigencia= → "Llave actualizada" | 400. La llave NO viaja en la respuesta.</summary>
    [HttpGet("genkey")]
    public async Task<IActionResult> GenerarLlave([FromQuery] string? banco, [FromQuery] string? vigencia, CancellationToken ct)
    {
        var actualizada = await _service.GenerarLlaveAsync(banco, vigencia, ct);
        return actualizada
            ? Texto(200, ContractXml.MsgLlaveActualizada)
            : Texto(400, ContractXml.MsgNoSePuedeActualizarLlave);
    }

    private ContentResult Xml(int statusCode, string body) => new()
    {
        Content = body,
        ContentType = ContractXml.ContentTypeXml,
        StatusCode = statusCode,
    };

    // El WS viejo negociaba estas respuestas String como application/xml
    // (@Produces con XML primero + StringProvider de Jersey).
    private ContentResult Texto(int statusCode, string body) => new()
    {
        Content = body,
        ContentType = ContractXml.ContentTypeXml,
        StatusCode = statusCode,
    };
}
