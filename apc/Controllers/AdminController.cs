using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIAD.Data;
using apc.Data;

namespace apc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SiadDbContext _siadDbContext;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SiadDbContext siadDbContext,
            ILogger<AdminController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _siadDbContext = siadDbContext;
            _logger = logger;
        }

        /// <summary>
        /// Ejecuta el seed de datos iniciales (usuario admin + empresa demo)
        /// SOLO en ambiente Development
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> Seed()
        {
            try
            {
                _logger.LogInformation("Iniciando seed de datos...");
                await DatabaseInitializer.SeedAsync(_userManager, _roleManager, _siadDbContext);
                _logger.LogInformation("Seed completado exitosamente");
                
                return Ok(new { message = "Seed ejecutado exitosamente. Usuario: admin@siad-demo.com, Contraseña: Admin123$" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar seed");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}
