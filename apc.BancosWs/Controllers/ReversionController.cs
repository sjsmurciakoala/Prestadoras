using apc.BancosWs.Contrato;
using Microsoft.AspNetCore.Mvc;
using SIAD.Services.BancosWs;

namespace apc.BancosWs.Controllers;

/// <summary>
/// Réplica de SrvReversion (contrato §2.6). La búsqueda es por referencia
/// (igual que el WS viejo); /servicios y /otros solo difieren en el mensaje
/// cuando la referencia ya fue reversada.
/// </summary>
[ApiController]
[Route("simafi/api/reversion")]
public sealed class ReversionController : ControllerBase
{
    private readonly IBancosWsService _service;
    private readonly ILogger<ReversionController> _logger;

    public ReversionController(IBancosWsService service, ILogger<ReversionController> logger)
    {
        _service = service;
        _logger = logger;
    }

    [HttpPost("servicios")]
    public Task<IActionResult> Servicios(CancellationToken ct) =>
        ReversarAsync(mensajeYaReversada: ContractXml.MsgReferenciaYaReversada, ct);

    [HttpPost("otros")]
    public Task<IActionResult> Otros(CancellationToken ct) =>
        ReversarAsync(mensajeYaReversada: ContractXml.MsgNoExisteReferenciaReversar, ct);

    private async Task<IActionResult> ReversarAsync(string mensajeYaReversada, CancellationToken ct)
    {
        var reversion = await PagoXmlRequest.LeerAsync(Request.Body, ct);
        if (reversion is null)
        {
            return Error(true, ContractXml.MsgProblemaServidor);
        }

        // Orden EXACTO de validaciones del WS viejo (SrvReversion): la clave
        // acá SÍ valida vacío (isEmpty), a diferencia del pago.
        if (string.IsNullOrEmpty(reversion.FechaRegistro))
        {
            return Error(true, ContractXml.MsgFechaRegistroVacia);
        }

        if (string.IsNullOrEmpty(reversion.Referencia))
        {
            return Error(true, ContractXml.MsgReferenciaVacia);
        }

        if (string.IsNullOrEmpty(reversion.Banco))
        {
            return Error(true, ContractXml.MsgBancoVacio);
        }

        if (string.IsNullOrEmpty(reversion.Clave))
        {
            return Error(true, ContractXml.MsgClaveVacia);
        }

        try
        {
            var resultado = await _service.ReversarAsync(reversion.Referencia, ct);

            return resultado.Status switch
            {
                "OK" => Xml(200, ContractXml.Mensaje(200, error: false, ContractXml.MsgReversionExitosa)),
                "NO_EXISTE" => Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgNoExisteReferenciaReversar)),
                "YA_REVERSADA" => Xml(400, ContractXml.Mensaje(400, error: false, mensajeYaReversada)),
                _ => Error(true, ContractXml.MsgNoSePuedeReversar),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reversión WS bancario falló: referencia {Referencia}.", reversion.Referencia);
            return Error(true, ContractXml.MsgNoSePuedeReversar);
        }
    }

    private ContentResult Error(bool error, string mensaje) =>
        Xml(400, ContractXml.Mensaje(400, error, mensaje));

    private ContentResult Xml(int statusCode, string body) => new()
    {
        Content = body,
        ContentType = ContractXml.ContentTypeXml,
        StatusCode = statusCode,
    };
}
