using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Tenancy;
using SIAD.Reports;
using SIAD.Services.Bancos;
using apc.Security;

namespace apc.Controllers.Bancos;

[ApiController]
[Route("api/bancos/cheques")]
[ModuleAuthorize(PermissionModules.Bancos)]
public sealed class ChequesController : ControllerBase
{
    private readonly IChequesService chequesService;
    private readonly IBanTransaccionesService transaccionesService;
    private readonly ICompanyAccessValidator accessValidator;
    private readonly ICurrentCompanyService currentCompanyService;

    public ChequesController(
        IChequesService chequesService,
        IBanTransaccionesService transaccionesService,
        ICompanyAccessValidator accessValidator,
        ICurrentCompanyService currentCompanyService)
    {
        this.chequesService = chequesService;
        this.transaccionesService = transaccionesService;
        this.accessValidator = accessValidator;
        this.currentCompanyService = currentCompanyService;
    }

    [HttpGet]
    public async Task<IActionResult> Buscar(
        [FromQuery] long? bancoId,
        [FromQuery] long? bancoCuentaId,
        [FromQuery] string? estado,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] decimal? numeroCheque,
        CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var filtro = new ChequeFilterDto
        {
            BancoId = bancoId,
            BancoCuentaId = bancoCuentaId,
            Estado = estado,
            Desde = desde,
            Hasta = hasta,
            NumeroCheque = numeroCheque
        };

        try
        {
            var cheques = await chequesService.BuscarAsync(filtro, ct);
            return Ok(cheques);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpGet("bitacora")]
    public async Task<IActionResult> BuscarBitacora(
        [FromQuery] long? bancoId,
        [FromQuery] long? bancoCuentaId,
        [FromQuery] string? accion,
        [FromQuery] DateTime? desde,
        [FromQuery] DateTime? hasta,
        [FromQuery] decimal? numeroCheque,
        CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var filtro = new ChequeBitacoraFilterDto
        {
            BancoId = bancoId,
            BancoCuentaId = bancoCuentaId,
            Accion = accion,
            Desde = desde,
            Hasta = hasta,
            NumeroCheque = numeroCheque
        };

        try
        {
            var eventos = await chequesService.BuscarBitacoraAsync(filtro, ct);
            return Ok(eventos);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    [HttpGet("proximo/{bancoCuentaId:long}")]
    public async Task<IActionResult> GetProximo(long bancoCuentaId, CancellationToken ct)
    {
        if (bancoCuentaId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar una cuenta bancaria valida." });
        }

        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var proximo = await chequesService.GetProximoAsync(bancoCuentaId, ct);
        return proximo is null ? NotFound() : Ok(proximo);
    }

    /// <summary>cheque_id vigente ligado a un movimiento bancario (para reimprimir desde el detalle de la transacción).</summary>
    [HttpGet("por-kardex/{banKardexId:long}")]
    public async Task<IActionResult> GetChequePorKardex(long banKardexId, CancellationToken ct)
    {
        if (banKardexId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un movimiento válido." });
        }

        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var chequeId = await chequesService.GetChequeIdVigentePorKardexAsync(banKardexId, ct);
        return Ok(new { chequeId });
    }

    [HttpPost("{bancoCuentaId:long}/anular-siguiente")]
    public async Task<IActionResult> AnularSiguiente(
        long bancoCuentaId,
        [FromBody] AnularNumeroChequeDto dto,
        CancellationToken ct)
    {
        if (bancoCuentaId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar una cuenta bancaria valida." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var numero = await chequesService.AnularSiguienteNumeroAsync(bancoCuentaId, dto.Motivo, usuario, ct);
            return Ok(numero);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
    }

    /// <summary>
    /// Emite un cheque manual (suelto, sin compromiso ni orden de pago): registra el
    /// movimiento bancario, la partida contable y el cheque con el siguiente numero
    /// de la cuenta.
    /// </summary>
    [HttpPost("manual")]
    public async Task<IActionResult> EmitirManual([FromBody] ChequeManualCreateDto dto, CancellationToken ct)
    {
        if (dto is null)
        {
            return BadRequest(new { detail = "Debe proporcionar los datos del cheque." });
        }

        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await transaccionesService.RegistrarChequeManualAsync(dto, usuario, ct);
            return Ok(resultado);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { detail = ex.Message });
        }
    }

    /// <summary>Comprobante interno del cheque (formato COMPAGOL) en PDF.</summary>
    [HttpGet("{chequeId:long}/comprobante/pdf")]
    public Task<IActionResult> GetComprobantePdf(long chequeId, CancellationToken ct)
        => GenerarPdfAsync(chequeId, ChequeFormato.Comprobante, ct);

    /// <summary>Cheque CORTO para el cliente (formato COMPAGOLG) en PDF, sobre el cheque preimpreso.</summary>
    [HttpGet("{chequeId:long}/cheque/pdf")]
    public Task<IActionResult> GetChequePdf(long chequeId, CancellationToken ct)
        => GenerarPdfAsync(chequeId, ChequeFormato.Cheque, ct);

    /// <summary>Cheque LARGO con detalle (cheque + comprobante en una hoja) en PDF.</summary>
    [HttpGet("{chequeId:long}/cheque-detalle/pdf")]
    public Task<IActionResult> GetChequeDetallePdf(long chequeId, CancellationToken ct)
        => GenerarPdfAsync(chequeId, ChequeFormato.ChequeDetalle, ct);

    private enum ChequeFormato { Comprobante, Cheque, ChequeDetalle }

    private async Task<IActionResult> GenerarPdfAsync(long chequeId, ChequeFormato formato, CancellationToken ct)
    {
        if (chequeId <= 0)
        {
            return BadRequest(new { detail = "Debe proporcionar un cheque valido." });
        }

        if (!await accessValidator.ValidarAccesoAsync(currentCompanyService.GetCompanyId(), ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";
        var datos = await chequesService.GetDatosImpresionAsync(chequeId, usuario, ct);
        if (datos is null)
        {
            return NotFound(new { detail = "El cheque no existe." });
        }

        var numero = decimal.Truncate(datos.NumeroCheque).ToString("000000");
        using var stream = new MemoryStream();
        string nombre;
        switch (formato)
        {
            case ChequeFormato.Comprobante:
            {
                using var report = new Rpt_Dev_Cheque_Comprobante(datos);
                report.ExportToPdf(stream);
                nombre = $"Comprobante-Cheque-{numero}.pdf";
                break;
            }
            case ChequeFormato.ChequeDetalle:
            {
                using var report = new Rpt_Dev_Cheque_Detalle(datos);
                report.ExportToPdf(stream);
                nombre = $"Cheque-Detalle-{numero}.pdf";
                break;
            }
            default:
            {
                using var report = new Rpt_Dev_Cheque(datos);
                report.ExportToPdf(stream);
                nombre = $"Cheque-{numero}.pdf";
                break;
            }
        }

        Response.Headers.ContentDisposition = $"inline; filename={nombre}";
        return File(stream.ToArray(), "application/pdf");
    }
}
