using apc.BancosWs.Contrato;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.BancosWs;
using SIAD.Services.BancosWs;

namespace apc.BancosWs.Controllers;

/// <summary>
/// Réplica de SrvConsulta (contrato §2.4). El tenant llega resuelto por el
/// middleware de credenciales. /otros responde con la misma consulta por
/// clave (en SIAD no existe el universo tipofa='O' de SIMAFI).
/// </summary>
[ApiController]
[Route("simafi/api/consulta")]
public sealed class ConsultaController : ControllerBase
{
    private readonly IBancosWsService _service;
    private readonly ILogger<ConsultaController> _logger;

    public ConsultaController(IBancosWsService service, ILogger<ConsultaController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpGet("servicios/{clave}")]
    public Task<IActionResult> Servicios(string clave, [FromQuery] string? abonado, CancellationToken ct) =>
        ConsultarAsync(clave, ct);

    [HttpGet("otros/{clave}")]
    public Task<IActionResult> Otros(string clave, CancellationToken ct) =>
        ConsultarAsync(clave, ct);

    private async Task<IActionResult> ConsultarAsync(string clave, CancellationToken ct)
    {
        try
        {
            var consulta = await _service.ConsultarAsync(clave, ct);
            return consulta.Resultado switch
            {
                BancosWsConsultaResultado.SinRegistro =>
                    Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgNoExisteRegistro)),
                BancosWsConsultaResultado.SinPendientes =>
                    Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgNoHayPagosPendientes)),
                BancosWsConsultaResultado.Vencidas =>
                    Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgFacturasVencidas)),
                _ => Xml(200, ContractXml.Factura(consulta)),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Consulta WS bancario falló para clave {Clave}.", clave);
            return Xml(400, ContractXml.Mensaje(400, error: true, ContractXml.MsgProblemaServidor));
        }
    }

    private ContentResult Xml(int statusCode, string body) => new()
    {
        Content = body,
        ContentType = ContractXml.ContentTypeXml,
        StatusCode = statusCode,
    };
}
