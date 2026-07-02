using Microsoft.AspNetCore.Identity;
using apc.Data;

namespace apc.Helpers
{
    /// <summary>
    /// Herramienta para reparar problemas de usuario en base de datos
    /// Uso: await UserRepairHelper.ReparaUsuarioAsync(userManager, "admin@siad-demo.com");
    /// </summary>
    public static class UserRepairHelper
    {
        public static async Task<RepairResult> ReparaUsuarioAsync(
            UserManager<ApplicationUser> userManager,
            string email)
        {
            var resultado = new RepairResult { Email = email };

            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                resultado.Exitoso = false;
                resultado.Mensaje = $"❌ Usuario con email '{email}' no existe";
                return resultado;
            }

            var reparaciones = new List<string>();

            // 1. Confirmar email
            if (!user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                reparaciones.Add("✓ Email confirmado");
            }

            // 2. Desbloquear usuario
            if (user.LockoutEnd.HasValue && user.LockoutEnd > DateTimeOffset.UtcNow)
            {
                user.LockoutEnd = null;
                reparaciones.Add("✓ Usuario desbloqueado");
            }

            // 3. Resetear intentos fallidos
            if (user.AccessFailedCount > 0)
            {
                user.AccessFailedCount = 0;
                reparaciones.Add("✓ Contador de intentos fallidos reseteado");
            }

            if (reparaciones.Any())
            {
                var result = await userManager.UpdateAsync(user);
                if (!result.Succeeded)
                {
                    resultado.Exitoso = false;
                    resultado.Mensaje = $"❌ Error actualizando usuario: {string.Join(", ", result.Errors.Select(e => e.Description))}";
                    return resultado;
                }

                resultado.Exitoso = true;
                resultado.Mensaje = "✓ Usuario reparado: " + string.Join("; ", reparaciones);
            }
            else
            {
                resultado.Exitoso = true;
                resultado.Mensaje = "✓ Usuario ya está en buen estado";
            }

            return resultado;
        }

        public class RepairResult
        {
            public bool Exitoso { get; set; }
            public string Email { get; set; }
            public string Mensaje { get; set; }
        }
    }
}
