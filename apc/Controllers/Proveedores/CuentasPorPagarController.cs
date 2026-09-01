using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Services.Proveedores;
using apc.Security;

namespace apc.Controllers.Proveedores;

/// <summary>
/// Cuentas por pagar unificadas: facturas de compra y compromisos en un solo listado, y el pago
/// de varios documentos en una sola operación. Thin: valida, resuelve usuario y delega en
/// <see cref="ICuentasPorPagarService"/>.
/// <para>
/// Autorización (D5 del plan): <b>ver</b> la lista completa basta con el módulo Compras —es la
/// misma deuda que ya se veía en «Pagos a proveedores»—, pero <b>pagar un compromiso</b> sigue
/// exigiendo el permiso de Contabilidad, igual que su propia pantalla. Por eso el lote lo
/// comprueba solo cuando lleva compromisos dentro.
/// </para>
/// </summary>
[ApiController]
[Route("api/proveedores/cuentas-por-pagar")]
[ModuleAuthorize(PermissionModules.Compras)]
public sealed class CuentasPorPagarController : ControllerBase
{
    private readonly ICuentasPorPagarService _service;
    private readonly IAuthorizationService _authorizationService;

    public CuentasPorPagarController(
        ICuentasPorPagarService service,
        IAuthorizationService authorizationService)
    {
        _service = service;
        _authorizationService = authorizationService;
    }

    private string Usuario => User?.Identity?.Name ?? "system";

    // Mismo permiso que exige la pantalla de abonar compromisos, para que lo que se ve y lo
    // que se puede hacer no puedan separarse. Super Administrador lo salta por la policy.
    private async Task<bool> PuedePagarCompromisosAsync()
        => (await _authorizationService.AuthorizeAsync(
                User, PermissionNames.Contabilidad.View)).Succeeded;

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] CxpUnificadaFilterDto filtro, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(await _service.ListarAsync(filtro, ct));
    }

    [HttpGet("resumen")]
    public async Task<IActionResult> GetResumen([FromQuery] CxpUnificadaFilterDto filtro, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        return Ok(await _service.ObtenerResumenAsync(filtro, ct));
    }

    /// <summary>
    /// Paga varios documentos de una vez. Se registran todos o ninguno: el servicio los envuelve
    /// en una sola transacción.
    /// </summary>
    [HttpPost("lote")]
    [ModuleAuthorize(PermissionModules.Compras, PermissionAction.Create)]
    public async Task<IActionResult> PagarLote([FromBody] CxpLoteUpsertDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var llevaCompromisos = dto.Lineas?.Any(l => l.Origen == OrigenDocumentoProveedor.Compromiso) == true;
        if (llevaCompromisos && !await PuedePagarCompromisosAsync())
        {
            return Problem(
                detail: "El lote incluye compromisos y su usuario no tiene permiso de Contabilidad. Quite los compromisos de la selección o pídale a Contabilidad que los pague.",
                statusCode: StatusCodes.Status403Forbidden);
        }

        try
        {
            return Ok(await _service.PagarLoteAsync(dto, Usuario, ct));
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
