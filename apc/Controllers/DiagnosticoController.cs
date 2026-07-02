using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using apc.Data;

namespace apc.Controllers
{
    /// <summary>
    /// Controlador administrativo para diagnóstico y reparación de problemas de autenticación.
    /// SOLO DISPONIBLE EN DEVELOPMENT
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DiagnosticoController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<DiagnosticoController> _logger;

        public DiagnosticoController(
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            ILogger<DiagnosticoController> logger)
        {
            _userManager = userManager;
            _env = env;
            _logger = logger;
        }

        /// <summary>
        /// Obtiene el estado de confirmación de todos los usuarios
        /// </summary>
        [HttpGet("usuarios")]
        public async Task<IActionResult> ObtenerDiagnosticoUsuarios()
        {
            if (!_env.IsDevelopment())
                return Forbid("Este endpoint solo está disponible en desarrollo");

            var users = _userManager.Users.Select(u => new
            {
                u.Id,
                u.UserName,
                u.Email,
                u.EmailConfirmed,
                u.LockoutEnabled,
                u.LockoutEnd,
                u.TwoFactorEnabled,
                PasswordHash = string.IsNullOrWhiteSpace(u.PasswordHash) ? "❌ NO SET" : "✓ SET"
            }).ToList();

            return Ok(new
            {
                total = users.Count,
                usuarios = users,
                diagnostico = users.Any(u => !u.EmailConfirmed)
                    ? "⚠️ PROBLEMA: Hay usuarios con EmailConfirmed = false"
                    : "✓ Todos los usuarios están confirmados"
            });
        }

        /// <summary>
        /// Confirma todos los usuarios que no estén confirmados
        /// </summary>
        [HttpPost("confirmar-todos-usuarios")]
        public async Task<IActionResult> ConfirmarTodosUsuarios()
        {
            if (!_env.IsDevelopment())
                return Forbid("Este endpoint solo está disponible en desarrollo");

            var usersNoConfirmados = _userManager.Users
                .Where(u => !u.EmailConfirmed)
                .ToList();

            var confirmados = 0;
            foreach (var user in usersNoConfirmados)
            {
                user.EmailConfirmed = true;
                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    confirmados++;
                    _logger.LogInformation($"Usuario {user.UserName} ({user.Email}) confirmado");
                }
                else
                {
                    _logger.LogError($"Error confirmando usuario {user.UserName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                }
            }

            return Ok(new
            {
                mensaje = $"Se confirmaron {confirmados} usuarios",
                confirmados,
                total = usersNoConfirmados.Count
            });
        }

        /// <summary>
        /// Confirma un usuario específico por email
        /// </summary>
        [HttpPost("confirmar-usuario/{email}")]
        public async Task<IActionResult> ConfirmarUsuario(string email)
        {
            if (!_env.IsDevelopment())
                return Forbid("Este endpoint solo está disponible en desarrollo");

            var user = await _userManager.FindByEmailAsync(email);
            if (user is null)
                return NotFound($"Usuario con email {email} no encontrado");

            if (user.EmailConfirmed)
                return Ok(new { mensaje = "El usuario ya está confirmado" });

            user.EmailConfirmed = true;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
                return BadRequest(new { errores = result.Errors.Select(e => e.Description) });

            return Ok(new
            {
                mensaje = $"Usuario {user.UserName} ({user.Email}) confirmado exitosamente",
                usuario = new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.EmailConfirmed
                }
            });
        }

        /// <summary>
        /// Diagnóstico detallado de un usuario específico
        /// </summary>
        [HttpGet("verificar-usuario/{email}")]
        public async Task<IActionResult> VerificarUsuario(string email)
        {
            if (!_env.IsDevelopment())
                return Forbid("Este endpoint solo está disponible en desarrollo");

            _logger.LogInformation($"Verificando usuario: {email}");

            var user = await _userManager.FindByEmailAsync(email);
            
            if (user is null)
            {
                _logger.LogWarning($"Usuario {email} NO ENCONTRADO");
                return Ok(new
                {
                    encontrado = false,
                    email = email,
                    mensaje = "❌ Usuario NO encontrado en la base de datos"
                });
            }

            _logger.LogInformation($"Usuario encontrado: {user.UserName}");

            var roles = await _userManager.GetRolesAsync(user);
            var claims = await _userManager.GetClaimsAsync(user);
            var isLockedOut = user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow;

            return Ok(new
            {
                encontrado = true,
                usuario = new
                {
                    user.Id,
                    user.UserName,
                    user.Email,
                    user.NormalizedUserName,
                    user.NormalizedEmail,
                    user.EmailConfirmed,
                    user.PhoneNumberConfirmed,
                    user.TwoFactorEnabled,
                    user.LockoutEnabled,
                    user.LockoutEnd,
                    isLockedOut,
                    tieneContraseña = !string.IsNullOrWhiteSpace(user.PasswordHash),
                    roles = roles,
                    claims = claims.Select(c => new { c.Type, c.Value })
                },
                diagnostico = new
                {
                    problema_email_no_confirmado = !user.EmailConfirmed,
                    problema_usuario_bloqueado = isLockedOut,
                    problema_sin_contraseña = string.IsNullOrWhiteSpace(user.PasswordHash),
                    problema_sin_roles = !roles.Any(),
                    mensaje_resumido = GenerarMensajeDiagnostico(user, isLockedOut, roles)
                }
            });
        }

        private string GenerarMensajeDiagnostico(ApplicationUser user, bool isLockedOut, IList<string> roles)
        {
            var problemas = new List<string>();

            if (!user.EmailConfirmed)
                problemas.Add("❌ Email NO confirmado");

            if (isLockedOut)
                problemas.Add("❌ Usuario bloqueado");

            if (string.IsNullOrWhiteSpace(user.PasswordHash))
                problemas.Add("❌ Sin contraseña");

            if (!roles.Any())
                problemas.Add("⚠️  Sin roles asignados");

            if (user.TwoFactorEnabled)
                problemas.Add("ℹ️  2FA habilitado (puede requerir verificación)");

            return problemas.Any()
                ? "Problemas encontrados: " + string.Join("; ", problemas)
                : "✓ Todo parece estar bien";
        }

        /// <summary>
        /// Repara automáticamente problemas de usuario
        /// </summary>
        [HttpPost("reparar-usuario/{email}")]
        public async Task<IActionResult> RepararUsuario(string email)
        {
            if (!_env.IsDevelopment())
                return Forbid("Este endpoint solo está disponible en desarrollo");

            var resultado = await apc.Helpers.UserRepairHelper.ReparaUsuarioAsync(_userManager, email);
            
            return Ok(new
            {
                exitoso = resultado.Exitoso,
                email = resultado.Email,
                mensaje = resultado.Mensaje
            });
        }
    }
}
