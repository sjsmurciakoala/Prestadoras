using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;
using SIAD.Services.Contabilidad;
using apc.Security;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad")]
[ModuleAuthorize(PermissionModules.Contabilidad)]
public sealed class ConfiguracionSistemaController : ControllerBase
{
    private readonly SiadDbContext dbContext;
    private readonly ICompanyAccessValidator accessValidator;
    private readonly IConfiguracionSistemaService configuracionService;

    public ConfiguracionSistemaController(SiadDbContext dbContext, ICompanyAccessValidator accessValidator,
        IConfiguracionSistemaService configuracionService)
    {
        this.dbContext = dbContext;
        this.accessValidator = accessValidator;
        this.configuracionService = configuracionService;
    }

    /// <summary>
    /// Obtiene la configuracion del sistema de una empresa
    /// </summary>
    [HttpGet("configuracion/{companyId}")]
    public async Task<IActionResult> ObtenerConfiguracion(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var config = await configuracionService.ObtenerAsync(companyId, ct);

            if (config == null)
            {
                var hoy = DateTime.Today;
                return Ok(new ConfiguracionSistemaDto
                {
                    Principal = new ConfiguracionPrincipalDto
                    {
                        FechaInicioEjercicio = new DateTime(hoy.Year, 1, 1),
                        FechaFinEjercicio = new DateTime(hoy.Year, 12, 31),
                        MesesCalculados = 12,
                        SeparadorCodigo = "-",
                        FormatoCuentas = "###-###-##",
                        FormatoCentros = "###-##",
                        SymbolSaldoAcreedor = "CR",
                        MontoMaximo = 99999999999m,
                        FrecuenciaDepreciacion = "Mensual",
                        UltimaDepreciacion = null
                    },
                    CuentasUtilidad = new CuentasUtilidadDto(),
                    EstadoSituacionFinanciera = new EstadoSituacionFinancieraDto(),
                    LineasResultado = new List<LineaResultadoDto>(),
                    LineasBalance = new List<BalanceSheetLineDto>(),
                    Correlativos = new List<CorrelativoDto>()
                });
            }

            return Ok(config);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener la configuracion: {ex.Message}" });
        }
    }

    /// <summary>
    /// Guarda la configuracion del sistema
    /// </summary>
    [SuppressModelStateInvalidFilter]
    [HttpPost("configuracion/{companyId}")]
    public async Task<IActionResult> GuardarConfiguracion(long companyId, [FromBody] ConfiguracionSistemaDto dto, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        var existePlanCuentas = await configuracionService.ExistePlanCuentasAsync(companyId, ct);
        if (!existePlanCuentas)
        {
            NormalizarSeccionesDependientesDeCuentas(dto);
        }

        if (!ModelState.IsValid)
        {
            if (!existePlanCuentas)
            {
                LimpiarErroresSeccion(ModelState,
                    nameof(ConfiguracionSistemaDto.CuentasUtilidad),
                    nameof(ConfiguracionSistemaDto.EstadoSituacionFinanciera),
                    nameof(ConfiguracionSistemaDto.LineasResultado),
                    nameof(ConfiguracionSistemaDto.LineasBalance));
            }

            if (!ModelState.IsValid)
            {
                return ValidationProblem(ModelState);
            }
        }

        var usuario = User?.Identity?.Name ?? "system";

        try
        {
            var resultado = await configuracionService.GuardarAsync(companyId, dto, usuario, ct);
            return Ok(resultado);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { detail = ex.Message });
        }
        catch (DbUpdateException ex)
        {
            var detalle = BuildDbUpdateMessage(ex);
            return BadRequest(new { detail = detalle });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar la configuracion: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene la lista de cuentas contables disponibles
    /// </summary>
    [HttpGet("{companyId}/cuentas")]
    public async Task<IActionResult> ListarCuentas(long companyId, CancellationToken ct)
    {
        if (!await accessValidator.ValidarAccesoAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var cuentas = await dbContext.con_plan_cuentas
                .AsNoTracking()
                .Where(c => c.company_id == companyId)
                .OrderBy(c => c.code)
                .Select(c => new CuentaContableLookupDto
                {
                    AccountId = c.account_id,
                    Code = c.code,
                    Description = c.name
                })
                .ToListAsync(cancellationToken: ct);

            return Ok(cuentas);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al cargar las cuentas: {ex.Message}" });
        }
    }

    private static void NormalizarSeccionesDependientesDeCuentas(ConfiguracionSistemaDto dto)
    {
        dto.CuentasUtilidad = new CuentasUtilidadDto();
        dto.EstadoSituacionFinanciera = new EstadoSituacionFinancieraDto();
        dto.LineasResultado = new List<LineaResultadoDto>();
        dto.LineasBalance = new List<BalanceSheetLineDto>();
    }

    private static void LimpiarErroresSeccion(ModelStateDictionary modelState, params string[] secciones)
    {
        var keys = modelState.Keys
            .Where(key => secciones.Any(seccion =>
                string.Equals(key, seccion, StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith($"{seccion}.", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith($"{seccion}[", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        foreach (var key in keys)
        {
            modelState.Remove(key);
        }
    }

    private static string BuildDbUpdateMessage(DbUpdateException ex)
    {
        if (ex.GetBaseException() is PostgresException pg)
        {
            return BuildPostgresMessage(pg);
        }

        var raiz = ex.GetBaseException()?.Message ?? ex.Message;
        return $"Error al guardar la configuracion: {raiz}";
    }

    private static string BuildPostgresMessage(PostgresException pg)
    {
        var column = pg.ColumnName;
        var table = pg.TableName;
        var constraint = pg.ConstraintName;
        var friendly = GetFriendlyColumnName(column) ?? column;

        switch (pg.SqlState)
        {
            case PostgresErrorCodes.NotNullViolation:
                return $"Falta un valor obligatorio{FormatFieldSuffix(friendly)}.";
            case PostgresErrorCodes.UniqueViolation:
                return FormatConstraintMessage("Ya existe un registro con el mismo valor", friendly, table, constraint);
            case PostgresErrorCodes.ForeignKeyViolation:
                return FormatConstraintMessage("Referencia invalida: el valor no existe en la tabla relacionada", friendly, table, constraint);
            case PostgresErrorCodes.CheckViolation:
                return FormatConstraintMessage("El valor no cumple una regla de validacion", friendly, table, constraint);
            case PostgresErrorCodes.StringDataRightTruncation:
                return $"El valor es demasiado largo{FormatFieldSuffix(friendly)}.";
            case PostgresErrorCodes.NumericValueOutOfRange:
                return $"El valor numerico esta fuera de rango{FormatFieldSuffix(friendly)}.";
            case PostgresErrorCodes.InvalidTextRepresentation:
                return $"El formato del valor no es valido{FormatFieldSuffix(friendly)}.";
            default:
                return FormatConstraintMessage($"Error de base de datos ({pg.SqlState})", friendly, table, constraint, pg.MessageText);
        }
    }

    private static string FormatFieldSuffix(string? fieldName)
    {
        return string.IsNullOrWhiteSpace(fieldName) ? string.Empty : $" en '{fieldName}'";
    }

    private static string FormatConstraintMessage(string prefix, string? fieldName, string? tableName, string? constraintName, string? message = null)
    {
        var fieldSuffix = FormatFieldSuffix(fieldName);
        var parts = new List<string>();

        if (!string.IsNullOrWhiteSpace(tableName))
        {
            parts.Add($"tabla {tableName}");
        }

        if (!string.IsNullOrWhiteSpace(constraintName))
        {
            parts.Add($"constraint {constraintName}");
        }

        var context = parts.Count > 0 ? $" ({string.Join(", ", parts)})" : string.Empty;
        var detail = !string.IsNullOrWhiteSpace(message) ? $": {message}" : string.Empty;
        return $"{prefix}{fieldSuffix}{context}{detail}";
    }

    private static string? GetFriendlyColumnName(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName))
        {
            return null;
        }

        return columnName.ToLowerInvariant() switch
        {
            "fmt_ctas" => "Formato de cuentas",
            "formato_cuentas" => "Formato de cuentas",
            "fmt_centros" => "Formato de centros",
            "fmt_ctos" => "Formato de centros",
            "formato_centros" => "Formato de centros",
            "sep_codigo" => "Separador de codigo",
            "separador_codigo" => "Separador de codigo",
            "meses_calc" => "Meses calculados",
            "meses_calculados" => "Meses calculados",
            _ => null
        };
    }
}

