using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SIAD.Core.Constants;
using SIAD.Data;
using apc.Data;

namespace apc.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = AuthorizationPolicies.SuperAdmin)]
    public class AdminController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly SiadDbContext _siadDbContext;
        private readonly ILogger<AdminController> _logger;
        private readonly IWebHostEnvironment _environment;

        public AdminController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            SiadDbContext siadDbContext,
            ILogger<AdminController> logger,
            IWebHostEnvironment environment)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _siadDbContext = siadDbContext;
            _logger = logger;
            _environment = environment;
        }

        /// <summary>
        /// Ejecuta el seed de datos iniciales (usuario admin + empresa demo)
        /// SOLO en ambiente Development
        /// </summary>
        [HttpPost("seed")]
        public async Task<IActionResult> Seed()
        {
            if (!_environment.IsDevelopment())
            {
                return NotFound();
            }

            try
            {
                _logger.LogInformation("Iniciando seed de datos...");
                await DatabaseInitializer.SeedAsync(_userManager, _roleManager, _siadDbContext);
                _logger.LogInformation("Seed completado exitosamente");
                
                return Ok(new { message = "Seed ejecutado exitosamente. Usuario: admin@siad-demo.com, Contraseña: Admin123@" });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al ejecutar seed");
                return BadRequest(new { error = ex.Message });
            }
        }
    }
}

