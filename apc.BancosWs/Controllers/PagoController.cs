using apc.BancosWs.Contrato;
using apc.BancosWs.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.BancosWs;
using SIAD.Services.BancosWs;

namespace apc.BancosWs.Controllers;

/// <summary>
/// Réplica de SrvPago (contrato §2.5). Validaciones de campos en el ORDEN
/// EXACTO del WS viejo; el resto de la semántica (idempotencia, monto == total,
/// FIFO) vive en sp_ban_ws_pagar y acá solo se mapean los códigos al XML.
/// </summary>
[ApiController]
[Route("simafi/api/pago")]
public sealed class PagoController : ControllerBase
{
    private readonly IBancosWsService _service;
    private readonly BancosWsRequestContext _requestContext;

    public PagoController(IBancosWsService service, BancosWsRequestContext requestContext)
    {
        _service = service;
        _requestContext = requestContext;
    }

    /// <summary>Endpoint de prueba del WS viejo (sin auth, valores fijos).</summary>
    [HttpGet("dummy")]
    public IActionResult Dummy() => Xml(200, ContractXml.PagoDummy());

    [HttpPost("servicios")]
    public Task<IActionResult> Servicios([FromQuery] string? abonado, CancellationToken ct) =>
        PagarAsync(tipo: "S", validarMonto: true, ct);

    [HttpPost("otros")]
    public Task<IActionResult> Otros(CancellationToken ct) =>
        PagarAsync(tipo: "O", validarMonto: false, ct);

    private async Task<IActionResult> PagarAsync(string tipo, bool validarMonto, CancellationToken ct)
    {
        var pago = await PagoXmlRequest.LeerAsync(Request.Body, ct);
        if (pago is null)
        {
            return Error(true, ContractXml.MsgProblemaServidor);
        }

        // Orden EXACTO de validaciones del WS viejo (SrvPago).
        if (string.IsNullOrEmpty(pago.FechaRegistro))
        {
            return Error(true, ContractXml.MsgFechaRegistroVacia);
        }

        if (string.IsNullOrEmpty(pago.Referencia))
        {
            return Error(true, ContractXml.MsgReferenciaVacia);
        }

        if (string.IsNullOrEmpty(pago.Banco))
        {
            return Error(true, ContractXml.MsgBancoVacio);
        }

        if (pago.Clave is null)
        {
            return Error(true, ContractXml.MsgClaveVacia);
        }

        if (!pago.TryParseFechaRegistro(out var fechaRegistro) || !pago.TryParseMonto(out var monto))
        {
            return Error(true, ContractXml.MsgProblemaServidor);
        }

        try
        {
            var resultado = await _service.PagarAsync(
                new BancosWsPagoRequestDto
                {
                    Clave = pago.Clave,
                    Referencia = pago.Referencia,
                    Banco = pago.Banco,
                    Monto = monto,
                    FechaRegistro = fechaRegistro,
                    HoraRegistro = pago.ParseHoraRegistro(),
                    FechaEfectiva = pago.ParseFechaEfectiva(),
                    Sucursal = pago.Sucursal,
                    Cajero = pago.Cajero,
                    Tipo = tipo,
                    ValidarMonto = validarMonto,
                },
                _requestContext.BancoCuentaId,
                ct);

            return resultado.Status switch
            {
                // Replay del mismo pago: MISMA respuesta, cero duplicación (contrato F8 §4.2).
                "OK" or "IDEMPOTENTE" =>
                    Xml(200, ContractXml.Mensaje(200, error: false, ContractXml.MsgPagoExitoso)),
                "SIN_REGISTRO" =>
                    Error(true, ContractXml.MsgNoExisteRegistroPunto),
                "SIN_PENDIENTES" =>
                    Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgNoHayPagosPendientesPunto)),
                "MONTO_NO_COINCIDE" =>
                    Xml(400, ContractXml.Mensaje(400, error: false, ContractXml.MsgTotalNoCoincide)),
                // REFERENCIA_REVERSADA y cualquier código no esperado.
                _ => Error(true, ContractXml.MsgNoSePuedePagar),
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            return Error(true, ContractXml.MsgNoSePuedePagar);
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
