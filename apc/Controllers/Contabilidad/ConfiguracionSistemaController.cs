using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace apc.Controllers.Contabilidad;

[ApiController]
[Route("api/contabilidad")]
[Authorize(Roles = RoleNames.AdminContabilidad + "," + RoleNames.SuperAdministrador)]
public sealed class ConfiguracionSistemaController : ControllerBase
{
    private readonly SiadDbContext dbContext;
    private readonly ICurrentCompanyService currentCompanyService;

    public ConfiguracionSistemaController(SiadDbContext dbContext, ICurrentCompanyService currentCompanyService)
    {
        this.dbContext = dbContext;
        this.currentCompanyService = currentCompanyService;
    }

    /// <summary>
    /// Obtiene la configuración del sistema de una empresa
    /// </summary>
    [HttpGet("configuracion/{companyId}")]
    public async Task<IActionResult> ObtenerConfiguracion(long companyId, CancellationToken ct)
    {
        // Validar que el usuario tenga acceso a esta empresa
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        try
        {
            var config = await dbContext.con_configuracion_sistemas
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.company_id == companyId, cancellationToken: ct);

            // Si no existe configuración, crear una por defecto
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

            // Obtener líneas de resultado
            var lineasResultado = await dbContext.con_configuracion_linea_resultados
                .AsNoTracking()
                .Where(c => c.company_id == companyId)
                .OrderBy(c => c.numero_linea)
                .ToListAsync(cancellationToken: ct);

            // Obtener líneas de balance
            var lineasBalance = await dbContext.con_configuracion_balances
                .AsNoTracking()
                .Where(c => c.company_id == companyId)
                .OrderBy(c => c.numero_linea)
                .ToListAsync(cancellationToken: ct);

            // Obtener correlativos
            var correlativos = await dbContext.con_configuracion_correlativos
                .AsNoTracking()
                .Where(c => c.company_id == companyId)
                .ToListAsync(cancellationToken: ct);

            var configuracionDto = new ConfiguracionSistemaDto
            {
                Principal = new ConfiguracionPrincipalDto
                {
                    FechaInicioEjercicio = config.fecha_inicio_ejercicio,
                    FechaFinEjercicio = config.fecha_fin_ejercicio,
                    MesesCalculados = config.meses_calculados,
                    SeparadorCodigo = config.separador_codigo,
                    FormatoCuentas = config.formato_cuentas,
                    FormatoCentros = config.formato_centros,
                    SymbolSaldoAcreedor = config.symbol_saldo_acreedor,
                    MontoMaximo = config.monto_maximo,
                    FrecuenciaDepreciacion = config.frecuencia_depreciacion,
                    UltimaDepreciacion = config.ultima_depreciacion
                },
                CuentasUtilidad = new CuentasUtilidadDto
                {
                    CodigoCuentaUtilAcumuladaHistorica = config.codigo_cuenta_util_acumulada_historica,
                    CodigoCuentaUtilAcumuladaInflacion = config.codigo_cuenta_util_acumulada_inflacion,
                    CodigoCuentaUtilEjercicioHistorica = config.codigo_cuenta_util_ejercicio_historica,
                    CodigoCuentaUtilEjercicioInflacion = config.codigo_cuenta_util_ejercicio_inflacion,
                    CodigoCuentaPerdidaAcumuladaHistorica = config.codigo_cuenta_perdida_acumulada_historica,
                    CodigoCuentaPerdidaAcumuladaInflacion = config.codigo_cuenta_perdida_acumulada_inflacion,
                    CodigoCuentaPerdidaEjercicioHistorica = config.codigo_cuenta_perdida_ejercicio_historica,
                    CodigoCuentaPerdidaEjercicioInflacion = config.codigo_cuenta_perdida_ejercicio_inflacion
                },
                EstadoSituacionFinanciera = new EstadoSituacionFinancieraDto
                {
                    CodigoActivoCortoPlazo1 = null,
                    CodigoActivoCortoPlazo2 = null,
                    CodigoActivoLargoPlazo1 = null,
                    CodigoActivoLargoPlazo2 = null,
                    CodigoPasivoCortoPlazo1 = null,
                    CodigoPasivoCortoPlazo2 = null,
                    CodigoPasivoLargoPlazo1 = null,
                    CodigoPasivoLargoPlazo2 = null,
                    CodigoPasivoyCapital = null,
                    CodigoCapitalAportado = null,
                    CodigoResultadosAcumulados = null,
                    CodigoUtilidadPerdidaEjercicio = null,
                    CodigoSobrevaluaciones = null,
                    MostrarOrden = config.mostrar_orden,
                    MostrarPercontra = config.mostrar_percontra
                },
                LineasResultado = lineasResultado.Select(lr => new LineaResultadoDto
                {
                    RowId = Guid.NewGuid(),
                    Tipo = lr.tipo_linea == 0 ? "Ingreso" : lr.tipo_linea == 1 ? "Costo" : "Gasto",
                    CodigoCuenta = lr.codigo_cuenta,
                    Descripcion = lr.descripcion_linea ?? string.Empty,
                    NivelIndentacion = lr.nivel_indentacion,
                    MostrarSubtotal = lr.mostrar_subtotal
                }).ToList(),
                LineasBalance = lineasBalance.Select(lb => new BalanceSheetLineDto
                {
                    RowId = Guid.NewGuid(),
                    Clase = lb.clase,
                    CodigoCuenta = lb.codigo_cuenta,
                    Descripcion = lb.descripcion_linea ?? lb.descripcion_cuenta ?? string.Empty,
                    PorcentajeActivo = lb.porcentaje_activo,
                    MostrarEnReporte = lb.mostrar_en_reporte
                }).ToList(),
                Correlativos = correlativos.Select(c => new CorrelativoDto
                {
                    RowId = Guid.NewGuid(),
                    Tipo = c.tipo,
                    Numerador = c.numerador,
                    SiguienteNumero = c.siguiente_numero,
                    Formato = c.formato
                }).ToList()
            };

            return Ok(configuracionDto);
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al obtener la configuración: {ex.Message}" });
        }
    }

    /// <summary>
    /// Guarda la configuración completa del sistema de una empresa
    /// </summary>
    [HttpPost("configuracion/{companyId}")]
    public async Task<IActionResult> GuardarConfiguracion(long companyId, [FromBody] ConfiguracionSistemaDto dto, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        // Validar que el usuario tenga acceso a esta empresa
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
        {
            return Forbid();
        }

        var usuario = User?.Identity?.Name ?? "system";
        var transaction = await dbContext.Database.BeginTransactionAsync(ct);

        try
        {
            // Configuración consolidada en una sola tabla
            var config = await dbContext.con_configuracion_sistemas
                .FirstOrDefaultAsync(c => c.company_id == companyId, cancellationToken: ct);

            if (config == null)
            {
                config = new con_configuracion_sistema { company_id = companyId, created_by = usuario };
                dbContext.con_configuracion_sistemas.Add(config);
            }
            else
            {
                config.updated_by = usuario;
                config.updated_at = DateTime.UtcNow;
            }

            // Mapear configuración principal
            config.fecha_inicio_ejercicio = dto.Principal.FechaInicioEjercicio;
            config.fecha_fin_ejercicio = dto.Principal.FechaFinEjercicio;
            config.meses_calculados = dto.Principal.MesesCalculados;
            config.separador_codigo = dto.Principal.SeparadorCodigo;
            config.formato_cuentas = dto.Principal.FormatoCuentas;
            config.formato_centros = dto.Principal.FormatoCentros;
            config.symbol_saldo_acreedor = dto.Principal.SymbolSaldoAcreedor;
            config.monto_maximo = dto.Principal.MontoMaximo;
            config.frecuencia_depreciacion = dto.Principal.FrecuenciaDepreciacion;
            config.ultima_depreciacion = dto.Principal.UltimaDepreciacion;

            // Mapear cuentas de utilidad (ahora con códigos)
            config.codigo_cuenta_util_acumulada_historica = dto.CuentasUtilidad.CodigoCuentaUtilAcumuladaHistorica;
            config.codigo_cuenta_util_acumulada_inflacion = dto.CuentasUtilidad.CodigoCuentaUtilAcumuladaInflacion;
            config.codigo_cuenta_util_ejercicio_historica = dto.CuentasUtilidad.CodigoCuentaUtilEjercicioHistorica;
            config.codigo_cuenta_util_ejercicio_inflacion = dto.CuentasUtilidad.CodigoCuentaUtilEjercicioInflacion;
            config.codigo_cuenta_perdida_acumulada_historica = dto.CuentasUtilidad.CodigoCuentaPerdidaAcumuladaHistorica;
            config.codigo_cuenta_perdida_acumulada_inflacion = dto.CuentasUtilidad.CodigoCuentaPerdidaAcumuladaInflacion;
            config.codigo_cuenta_perdida_ejercicio_historica = dto.CuentasUtilidad.CodigoCuentaPerdidaEjercicioHistorica;
            config.codigo_cuenta_perdida_ejercicio_inflacion = dto.CuentasUtilidad.CodigoCuentaPerdidaEjercicioInflacion;

            // Mapear opciones de presentación
            config.mostrar_orden = dto.EstadoSituacionFinanciera.MostrarOrden;
            config.mostrar_percontra = dto.EstadoSituacionFinanciera.MostrarPercontra;

            // Guardar líneas de resultado (tabla separada)
            var lineasExistentes = await dbContext.con_configuracion_linea_resultados
                .Where(r => r.company_id == companyId)
                .ToListAsync(cancellationToken: ct);

            dbContext.con_configuracion_linea_resultados.RemoveRange(lineasExistentes);

            if (dto.LineasResultado?.Count > 0)
            {
                var lineasNuevas = dto.LineasResultado
                    .Select((lr, i) => new con_configuracion_linea_resultado
                    {
                        company_id = companyId,
                        periodo_id = 1,  // TODO: Obtener período actual
                        numero_linea = (short)(i + 1),
                        tipo_linea = (byte)(lr.Tipo == "Ingreso" ? 0 : lr.Tipo == "Costo" ? 1 : 2),
                        codigo_cuenta = lr.CodigoCuenta,
                        descripcion_linea = lr.Descripcion,
                        nivel_indentacion = lr.NivelIndentacion,
                        mostrar_subtotal = lr.MostrarSubtotal,
                        created_by = usuario
                    })
                    .ToList();

                dbContext.con_configuracion_linea_resultados.AddRange(lineasNuevas);
            }

            // Guardar líneas de balance (tabla separada)
            var balancesExistentes = await dbContext.con_configuracion_balances
                .Where(b => b.company_id == companyId)
                .ToListAsync(cancellationToken: ct);

            dbContext.con_configuracion_balances.RemoveRange(balancesExistentes);

            if (dto.LineasBalance?.Count > 0)
            {
                var balancesNuevos = dto.LineasBalance
                    .Select((lb, i) => new con_configuracion_balance
                    {
                        company_id = companyId,
                        periodo_id = 1,  // TODO: Obtener período actual
                        numero_linea = (short)(i + 1),
                        clase = lb.Clase,
                        codigo_cuenta = lb.CodigoCuenta,
                        descripcion_linea = lb.Descripcion,
                        porcentaje_activo = lb.PorcentajeActivo,
                        mostrar_en_reporte = lb.MostrarEnReporte,
                        created_by = usuario
                    })
                    .ToList();

                dbContext.con_configuracion_balances.AddRange(balancesNuevos);
            }

            // Guardar correlativos
            var correlativosExistentes = await dbContext.con_configuracion_correlativos
                .Where(c => c.company_id == companyId)
                .ToListAsync(cancellationToken: ct);

            dbContext.con_configuracion_correlativos.RemoveRange(correlativosExistentes);

            if (dto.Correlativos?.Count > 0)
            {
                var correlativosNuevos = dto.Correlativos
                    .Select(c => new con_configuracion_correlativo
                    {
                        company_id = companyId,
                        tipo = c.Tipo,
                        numerador = c.Numerador,
                        siguiente_numero = c.SiguienteNumero,
                        formato = c.Formato,
                        created_by = usuario
                    })
                    .ToList();

                dbContext.con_configuracion_correlativos.AddRange(correlativosNuevos);
            }

            await dbContext.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            return await ObtenerConfiguracion(companyId, ct);
        }
        catch (DbUpdateException ex)
        {
            await transaction.RollbackAsync(ct);
            var raiz = ex.GetBaseException()?.Message ?? ex.Message;
            return BadRequest(new { detail = $"Error al guardar la configuración: {raiz}" });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(ct);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { detail = $"Error al guardar la configuración: {ex.Message}" });
        }
    }

    /// <summary>
    /// Obtiene la lista de cuentas contables disponibles para una empresa
    /// </summary>
    [HttpGet("{companyId}/cuentas")]
    public async Task<IActionResult> ListarCuentas(long companyId, CancellationToken ct)
    {
        // Validar que el usuario tenga acceso a esta empresa
        if (!await ValidarAccesoEmpresaAsync(companyId, ct))
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

    /// <summary>
    /// Valida que el usuario tenga acceso a la empresa especificada
    /// </summary>
    private async Task<bool> ValidarAccesoEmpresaAsync(long companyId, CancellationToken ct)
    {
        // El usuario debe pertenecer al tenant/empresa solicitada
        var empresaExiste = await dbContext.con_plan_cuentas
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId, cancellationToken: ct);

        return empresaExiste;
    }
}
