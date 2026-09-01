using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Aprobaciones;
using SIAD.Services.Aprobaciones;
using apc.Data;
using apc.Security;

namespace apc.Controllers.Configuracion;

/// <summary>
/// Configuración de la aprobación por niveles: interruptor por documento, escalera de montos y
/// aprobadores. Thin: valida, delega en <see cref="IAprobacionConfigService"/> y traduce el error
/// de negocio a 400.
/// <para>
/// Trae además el <b>lookup de usuarios y roles</b> para elegir aprobadores. Vive aquí y no en
/// <c>UsuariosPortalController</c> —que exige Super Administrador— porque quien configura la
/// escalera necesita ver la lista sin ser superadmin, y aquí queda acotado a lo mínimo: nombre de
/// usuario y roles, filtrado por la empresa de la sesión.
/// </para>
/// </summary>
[ApiController]
[Route("api/configuracion/aprobaciones")]
[ModuleAuthorize(PermissionModules.Configuracion, PermissionResources.Configuracion.Aprobaciones)]
public sealed class AprobacionesConfigController : ControllerBase
{
    private readonly IAprobacionConfigService _config;
    private readonly UserManager<ApplicationUser> _usuarios;
    private readonly RoleManager<IdentityRole> _roles;

    public AprobacionesConfigController(
        IAprobacionConfigService config,
        UserManager<ApplicationUser> usuarios,
        RoleManager<IdentityRole> roles)
    {
        _config = config;
        _usuarios = usuarios;
        _roles = roles;
    }

    /// <summary>Configuración completa de un documento (interruptor + escalera + advertencias).</summary>
    [HttpGet("{documento}")]
    public async Task<IActionResult> Obtener(string documento, CancellationToken ct)
    {
        if (!DocumentoValido(documento)) return DocumentoDesconocido();
        return Ok(await _config.ObtenerAsync(documento, ct));
    }

    /// <summary>Enciende o apaga el control y fija la autoaprobación.</summary>
    [HttpPut("{documento}/control")]
    public async Task<IActionResult> GuardarControl(
        string documento, [FromBody] ControlRequest body, CancellationToken ct)
    {
        if (!DocumentoValido(documento)) return DocumentoDesconocido();

        return await EjecutarAsync(async () =>
        {
            await _config.GuardarControlAsync(documento, body?.Modo ?? 0, body?.PermiteAutoaprobacion ?? false, ct);
            return Ok(await _config.ObtenerAsync(documento, ct));
        });
    }

    /// <summary>Crea o actualiza un nivel de la escalera.</summary>
    [HttpPut("{documento}/niveles")]
    public async Task<IActionResult> GuardarNivel(
        string documento, [FromBody] AprobacionNivelConfigDto dto, CancellationToken ct)
    {
        if (!DocumentoValido(documento)) return DocumentoDesconocido();
        if (dto is null) return Problem(detail: "Faltan los datos del nivel.", statusCode: StatusCodes.Status400BadRequest);

        return await EjecutarAsync(async () => Ok(await _config.GuardarNivelAsync(documento, dto, ct)));
    }

    /// <summary>Elimina un nivel y, en cascada, sus aprobadores.</summary>
    [HttpDelete("niveles/{nivelId:int}")]
    public Task<IActionResult> EliminarNivel(int nivelId, CancellationToken ct)
        => EjecutarAsync(async () =>
        {
            var ok = await _config.EliminarNivelAsync(nivelId, ct);
            return ok ? Ok(new { success = true }) : (IActionResult)NotFound();
        });

    /// <summary>Agrega un aprobador (usuario o rol) a un nivel.</summary>
    [HttpPost("niveles/{nivelId:int}/aprobadores")]
    public Task<IActionResult> AgregarAprobador(
        int nivelId, [FromBody] AprobacionAprobadorConfigDto dto, CancellationToken ct)
        => EjecutarAsync(async () => Ok(await _config.AgregarAprobadorAsync(nivelId, dto, ct)));

    /// <summary>Quita un aprobador de su nivel.</summary>
    [HttpDelete("aprobadores/{aprobadorId:int}")]
    public Task<IActionResult> EliminarAprobador(int aprobadorId, CancellationToken ct)
        => EjecutarAsync(async () =>
        {
            var ok = await _config.EliminarAprobadorAsync(aprobadorId, ct);
            return ok ? Ok(new { success = true }) : (IActionResult)NotFound();
        });

    /// <summary>
    /// Usuarios del portal que pueden nombrarse aprobadores, <b>filtrados por la empresa de la
    /// sesión</b>: la empresa se resuelve del claim de cada usuario, no de un parámetro.
    /// </summary>
    [HttpGet("usuarios")]
    public async Task<IActionResult> ListarUsuarios(CancellationToken ct)
    {
        var companyClaim = User.FindFirst(TenantClaimTypes.CompanyId)?.Value;

        var usuarios = await _usuarios.Users.AsNoTracking().OrderBy(u => u.UserName).ToListAsync(ct);
        var resultado = new List<AprobadorUsuarioLookupDto>();

        foreach (var usuario in usuarios)
        {
            var claims = await _usuarios.GetClaimsAsync(usuario);
            string? empresaDelUsuario = null;

            foreach (var claim in claims)
            {
                if (claim.Type == TenantClaimTypes.CompanyId) { empresaDelUsuario = claim.Value; break; }
            }

            // Sin claim de empresa el usuario no opera en ninguna: no se ofrece como aprobador.
            if (string.IsNullOrEmpty(empresaDelUsuario) || empresaDelUsuario != companyClaim) continue;

            var roles = await _usuarios.GetRolesAsync(usuario);

            resultado.Add(new AprobadorUsuarioLookupDto
            {
                UserName = (usuario.UserName ?? string.Empty).ToLowerInvariant(),
                Email = usuario.Email,
                Roles = new List<string>(roles)
            });
        }

        return Ok(resultado);
    }

    /// <summary>Roles disponibles para autorizar un nivel completo (D3).</summary>
    [HttpGet("roles")]
    public async Task<IActionResult> ListarRoles(CancellationToken ct)
    {
        var roles = await _roles.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        var nombres = new List<string>();

        foreach (var rol in roles)
        {
            if (!string.IsNullOrWhiteSpace(rol.Name)) nombres.Add(rol.Name);
        }

        return Ok(nombres);
    }

    /// <summary>Documentos que admite el motor, con su etiqueta legible para el selector.</summary>
    [HttpGet("documentos")]
    public IActionResult ListarDocumentos()
        => Ok(new[]
        {
            new { Codigo = DocumentosAprobacion.OrdenCompra,   Descripcion = "Orden de compra",           Disponible = true  },
            new { Codigo = DocumentosAprobacion.Requisicion,   Descripcion = "Requisición de materiales", Disponible = true  },
            new { Codigo = DocumentosAprobacion.FacturaCompra, Descripcion = "Factura de compra",         Disponible = false },
            new { Codigo = DocumentosAprobacion.PagoProveedor, Descripcion = "Pago a proveedor",          Disponible = false }
        });

    public sealed class ControlRequest
    {
        public short Modo { get; set; }
        public bool PermiteAutoaprobacion { get; set; }
    }

    private static bool DocumentoValido(string documento)
        => documento is DocumentosAprobacion.OrdenCompra
                     or DocumentosAprobacion.FacturaCompra
                     or DocumentosAprobacion.PagoProveedor
                     or DocumentosAprobacion.Requisicion;

    private IActionResult DocumentoDesconocido()
        => Problem(detail: "El documento indicado no admite aprobación por niveles.",
                   statusCode: StatusCodes.Status400BadRequest);

    private async Task<IActionResult> EjecutarAsync(Func<Task<IActionResult>> accion)
    {
        try
        {
            return await accion();
        }
        catch (InvalidOperationException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
        catch (ArgumentException ex)
        {
            return Problem(detail: ex.Message, statusCode: StatusCodes.Status400BadRequest);
        }
    }
}
