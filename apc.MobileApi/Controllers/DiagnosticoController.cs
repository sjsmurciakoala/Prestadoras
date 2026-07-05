using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using SIAD.Core.DTOs.MobileApi;

namespace apc.MobileApi.Controllers;

/// <summary>
/// Diagnóstico de la API móvil (paridad funcional con Diagnostico del WS):
/// hora del servidor, conectividad a PostgreSQL, ambiente y versión. Abierto
/// (sin token) para healthcheck.
/// </summary>
[ApiController]
[Route("api/diagnostico")]
public sealed class DiagnosticoController : ControllerBase
{
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public DiagnosticoController(IConfiguration configuration, IWebHostEnvironment environment)
    {
        _configuration = configuration;
        _environment = environment;
    }

    /// <summary>Estado del servicio y de la base de datos.</summary>
    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var connString = _configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        var dto = new DiagnosticoDto
        {
            ServerTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
            Ambiente = _environment.EnvironmentName,
            Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0",
        };

        try
        {
            var builder = new NpgsqlConnectionStringBuilder(connString);
            dto.PostgresServer = builder.Host ?? string.Empty;
            dto.PostgresDatabase = builder.Database ?? string.Empty;

            await using var conn = new NpgsqlConnection(connString);
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync(ct);
            dto.PostgresOk = true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            dto.PostgresOk = false;
            dto.PostgresError = ex.Message;
        }

        return Ok(dto);
    }
}
