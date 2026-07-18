using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Services.Contabilidad;

namespace apc.Controllers.Contabilidad;

/// <summary>
/// Expone la máscara de cuentas contables de la empresa actual a cualquier usuario autenticado.
/// Sin permiso de módulo a propósito: la consumen pantallas de Bancos, Proveedores y Presupuesto
/// cuyos usuarios pueden no tener acceso al módulo Contabilidad.
/// </summary>
[ApiController]
[Route("api/contabilidad/formato-cuentas")]
[Authorize]
public sealed class AccountFormatController : ControllerBase
{
    private readonly IAccountFormatService accountFormatService;

    public AccountFormatController(IAccountFormatService accountFormatService)
    {
        this.accountFormatService = accountFormatService;
    }

    [HttpGet]
    public async Task<ActionResult<AccountFormatDto>> Obtener(CancellationToken ct)
    {
        var format = await accountFormatService.GetFormatAsync(ct);
        return Ok(new AccountFormatDto
        {
            FormatoCuentas = format.Mask,
            SeparadorCodigo = format.Separator
        });
    }
}
