using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Usuarios;
using apc.Data;

namespace apc.Controllers.Parametros;

[ApiController]
[Route("api/parametros/usuarios")]
[Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
public sealed class UsuariosPortalController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UsuariosPortalController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
    {
        var users = await _userManager.Users
            .AsNoTracking()
            .OrderBy(u => u.Email)
            .ToListAsync(ct);

        var result = new List<UsuarioPortalDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var companyClaim = claims.FirstOrDefault(c => c.Type == TenantClaimTypes.CompanyId);

            result.Add(new UsuarioPortalDto
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                EmailConfirmado = user.EmailConfirmed,
                Bloqueado = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
                CompanyId = long.TryParse(companyClaim?.Value, out var cid) ? cid : null,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Obtener(string id, CancellationToken ct)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(CrearProblem("Usuario no encontrado", $"No existe un usuario con el ID {id}."));
        }

        var roles = await _userManager.GetRolesAsync(user);
        var claims = await _userManager.GetClaimsAsync(user);
        var companyClaim = claims.FirstOrDefault(c => c.Type == TenantClaimTypes.CompanyId);

        return Ok(new UsuarioPortalDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            EmailConfirmado = user.EmailConfirmed,
            Bloqueado = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow,
            CompanyId = long.TryParse(companyClaim?.Value, out var cid) ? cid : null,
            Roles = roles.ToList()
        });
    }

    [HttpGet("roles")]
    public async Task<IActionResult> ListarRoles(CancellationToken ct)
    {
        var roles = await _roleManager.Roles
            .AsNoTracking()
            .OrderBy(r => r.Name)
            .Select(r => r.Name!)
            .ToListAsync(ct);

        return Ok(roles);
    }

    [HttpPost("roles/sync")]
    public async Task<IActionResult> SincronizarRoles(CancellationToken ct)
    {
        var rolesDefinidos = new[]
        {
            RoleNames.SuperAdministrador
        };

        var creados = new List<string>();
        foreach (var roleName in rolesDefinidos)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(roleName));
                if (result.Succeeded)
                {
                    creados.Add(roleName);
                }
            }
        }

        // Asegurar permisos completos para Super Administrador
        var superAdminRole = await _roleManager.FindByNameAsync(RoleNames.SuperAdministrador);
        if (superAdminRole is not null)
        {
            var claims = await _roleManager.GetClaimsAsync(superAdminRole);
            var actuales = claims
                .Where(c => c.Type == PermissionClaimTypes.Permission)
                .Select(c => c.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var permiso in PermissionNames.All)
            {
                if (!actuales.Contains(permiso))
                {
                    await _roleManager.AddClaimAsync(superAdminRole,
                        new System.Security.Claims.Claim(PermissionClaimTypes.Permission, permiso));
                }
            }
        }

        return Ok(new { mensaje = $"Roles sincronizados. Creados: {creados.Count}", creados });
    }

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] CrearUsuarioPortalDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CrearProblem("Validación fallida",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))));
        }

        var existente = await _userManager.FindByEmailAsync(dto.Email);
        if (existente is not null)
        {
            return BadRequest(CrearProblem("Usuario duplicado", $"Ya existe un usuario con el correo {dto.Email}."));
        }

        var user = new ApplicationUser
        {
            UserName = dto.Email,
            Email = dto.Email,
            EmailConfirmed = true
        };

        var createResult = await _userManager.CreateAsync(user, dto.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(CrearProblem("Error al crear usuario",
                string.Join("; ", createResult.Errors.Select(e => e.Description))));
        }

        // Asignar claim de empresa
        if (dto.CompanyId.HasValue && dto.CompanyId.Value > 0)
        {
            await _userManager.AddClaimAsync(user,
                new System.Security.Claims.Claim(TenantClaimTypes.CompanyId, dto.CompanyId.Value.ToString()));
        }

        // Asignar roles
        if (dto.Roles.Count > 0)
        {
            var rolesValidos = new List<string>();
            foreach (var role in dto.Roles)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    rolesValidos.Add(role);
                }
            }

            if (rolesValidos.Count > 0)
            {
                await _userManager.AddToRolesAsync(user, rolesValidos);
            }
        }

        return Created($"api/parametros/usuarios/{user.Id}", new { user.Id, user.Email });
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> Actualizar(string id, [FromBody] EditarUsuarioPortalDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(CrearProblem("Validación fallida",
                string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage))));
        }

        var user = await _userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound(CrearProblem("Usuario no encontrado", $"No existe un usuario con el ID {id}."));
        }

        // Actualizar claim de empresa
        var claims = await _userManager.GetClaimsAsync(user);
        var companyClaim = claims.FirstOrDefault(c => c.Type == TenantClaimTypes.CompanyId);

        if (companyClaim is not null)
        {
            await _userManager.RemoveClaimAsync(user, companyClaim);
        }

        if (dto.CompanyId.HasValue && dto.CompanyId.Value > 0)
        {
            await _userManager.AddClaimAsync(user,
                new System.Security.Claims.Claim(TenantClaimTypes.CompanyId, dto.CompanyId.Value.ToString()));
        }

        // Actualizar roles
        var rolesActuales = await _userManager.GetRolesAsync(user);
        if (rolesActuales.Count > 0)
        {
            await _userManager.RemoveFromRolesAsync(user, rolesActuales);
        }

        if (dto.Roles.Count > 0)
        {
            var rolesValidos = new List<string>();
            foreach (var role in dto.Roles)
            {
                if (await _roleManager.RoleExistsAsync(role))
                {
                    rolesValidos.Add(role);
                }
            }

            if (rolesValidos.Count > 0)
            {
                await _userManager.AddToRolesAsync(user, rolesValidos);
            }
        }

        // Bloqueo/desbloqueo
        if (dto.Bloqueado)
        {
            await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        }
        else
        {
            await _userManager.SetLockoutEndDateAsync(user, null);
        }

        return Ok(new { mensaje = "Usuario actualizado correctamente." });
    }

    private static ProblemDetails CrearProblem(string titulo, string detalle) => new()
    {
        Title = titulo,
        Detail = detalle,
        Status = StatusCodes.Status400BadRequest
    };
}
