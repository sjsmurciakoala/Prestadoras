using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Roles;
using apc.Data;

namespace apc.Controllers.Parametros;

[ApiController]
[Route("api/parametros/roles")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class RolesPortalController : ControllerBase
{
    private readonly RoleManager<IdentityRole> _roleManager;

    public RolesPortalController(RoleManager<IdentityRole> roleManager)
    {
        _roleManager = roleManager;
    }

    [HttpGet("permisos")]
    public IActionResult ListarPermisos()
    {
        return Ok(PermissionNames.All);
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var result = new List<RoleDto>(roles.Count);
        foreach (var role in roles)
        {
            var permissions = await ObtenerPermisosAsync(role);
            result.Add(new RoleDto
            {
                Id = role.Id,
                Name = role.Name ?? string.Empty,
                Permissions = permissions
            });
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(string id, CancellationToken ct)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Rol no encontrado",
                Detail = $"No existe un rol con el ID {id}."
            });
        }

        var permissions = await ObtenerPermisosAsync(role);
        return Ok(new RoleDto
        {
            Id = role.Id,
            Name = role.Name ?? string.Empty,
            Permissions = permissions
        });
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CreateRoleDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var normalized = _roleManager.NormalizeKey(dto.Name);
        var existente = await _roleManager.Roles
            .AsNoTracking()
            .AnyAsync(r => r.NormalizedName == normalized, ct);
        if (existente)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Rol duplicado",
                Detail = $"Ya existe un rol con el nombre {dto.Name}."
            });
        }

        var role = new IdentityRole(dto.Name);
        var createResult = await _roleManager.CreateAsync(role);
        if (!createResult.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al crear rol",
                Detail = string.Join("; ", createResult.Errors.Select(e => e.Description))
            });
        }

        await SincronizarPermisosAsync(role, dto.Permissions);

        return Created($"api/parametros/roles/{role.Id}", new { role.Id, role.Name });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(string id, [FromBody] UpdateRoleDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound(new ProblemDetails
            {
                Title = "Rol no encontrado",
                Detail = $"No existe un rol con el ID {id}."
            });
        }

        if (EsSuperAdmin(role) && !string.Equals(dto.Name, RoleNames.SuperAdministrador, StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Rol protegido",
                Detail = "No se puede renombrar el rol Super Administrador."
            });
        }

        var normalized = _roleManager.NormalizeKey(dto.Name);
        var existeNombre = await _roleManager.Roles
            .AsNoTracking()
            .AnyAsync(r => r.Id != role.Id && r.NormalizedName == normalized, ct);
        if (existeNombre)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Rol duplicado",
                Detail = $"Ya existe un rol con el nombre {dto.Name}."
            });
        }

        role.Name = dto.Name;
        var updateResult = await _roleManager.UpdateAsync(role);
        if (!updateResult.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al actualizar rol",
                Detail = string.Join("; ", updateResult.Errors.Select(e => e.Description))
            });
        }

        await SincronizarPermisosAsync(role, dto.Permissions);
        return Ok(new { mensaje = "Rol actualizado correctamente." });
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Eliminar(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null)
        {
            return NotFound();
        }

        if (EsSuperAdmin(role))
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Rol protegido",
                Detail = "No se puede eliminar el rol Super Administrador."
            });
        }

        var result = await _roleManager.DeleteAsync(role);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Error al eliminar rol",
                Detail = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        return Ok(new { mensaje = "Rol eliminado correctamente." });
    }

    private async Task<List<string>> ObtenerPermisosAsync(IdentityRole role)
    {
        var claims = await _roleManager.GetClaimsAsync(role);
        return claims
            .Where(c => c.Type == PermissionClaimTypes.Permission)
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    private async Task SincronizarPermisosAsync(IdentityRole role, List<string> permisosSolicitados)
    {
        permisosSolicitados ??= [];
        var permitidos = new HashSet<string>(PermissionNames.All, StringComparer.OrdinalIgnoreCase);
        var permisosValidos = permisosSolicitados
            .Where(p => permitidos.Contains(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var claims = await _roleManager.GetClaimsAsync(role);
        var actuales = claims
            .Where(c => c.Type == PermissionClaimTypes.Permission)
            .Select(c => c.Value)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var claim in claims.Where(c => c.Type == PermissionClaimTypes.Permission))
        {
            if (!permisosValidos.Contains(claim.Value, StringComparer.OrdinalIgnoreCase))
            {
                await _roleManager.RemoveClaimAsync(role, claim);
            }
        }

        foreach (var permiso in permisosValidos)
        {
            if (!actuales.Contains(permiso))
            {
                await _roleManager.AddClaimAsync(role, new Claim(PermissionClaimTypes.Permission, permiso));
            }
        }
    }

    private static bool EsSuperAdmin(IdentityRole role)
    {
        return string.Equals(role.Name, RoleNames.SuperAdministrador, StringComparison.OrdinalIgnoreCase);
    }
}
