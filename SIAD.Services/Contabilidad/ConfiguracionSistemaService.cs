using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación del servicio de configuración del sistema contable.
/// </summary>
public sealed class ConfiguracionSistemaService : IConfiguracionSistemaService
{
    private readonly SiadDbContext _context;
    private readonly IPeriodoContableService _periodoService;

    public ConfiguracionSistemaService(SiadDbContext context, IPeriodoContableService periodoService)
    {
        _context = context;
        _periodoService = periodoService;
    }

    public async Task<ConfiguracionSistemaDto?> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        var config = await _context.con_configuracion_sistemas
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (config is null)
        {
            return null;
        }

        var lineasResultado = await _context.con_configuracion_linea_resultados
            .AsNoTracking()
            .Where(r => r.company_id == companyId)
            .OrderBy(r => r.numero_linea)
            .ToListAsync(ct);

        var lineasBalance = await _context.con_configuracion_balances
            .AsNoTracking()
            .Where(b => b.company_id == companyId)
            .OrderBy(b => b.numero_linea)
            .ToListAsync(ct);

        var correlativos = await _context.con_configuracion_correlativos
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .ToListAsync(ct);

        return MapearDto(config, lineasResultado, lineasBalance, correlativos);
    }

    public async Task<ConfiguracionSistemaDto> GuardarAsync(long companyId, ConfiguracionSistemaDto dto, string usuario,
        CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        ArgumentNullException.ThrowIfNull(dto);
        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

        // Validar que exista período abierto
        var existePeriodo = await _periodoService.ExistePeriodoAbiertoAsync(companyId, ct);
        if (!existePeriodo)
        {
            throw new InvalidOperationException(
                "No existe un período contable abierto. Cree uno antes de guardar la configuración.");
        }

        // Obtener período activo para guardar las líneas
        var periodoActivo = await _periodoService.ObtenerPeriodoActivoAsync(companyId, ct);
        if (periodoActivo is null)
        {
            throw new InvalidOperationException("No se pudo obtener el período activo.");
        }

        var ahora = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        try
        {
            // Obtener o crear configuración
            var config = await _context.con_configuracion_sistemas
                .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

            if (config is null)
            {
                config = new con_configuracion_sistema
                {
                    company_id = companyId,
                    created_at = ahora,
                    created_by = usuario
                };
                _context.con_configuracion_sistemas.Add(config);
            }
            else
            {
                config.updated_at = ahora;
                config.updated_by = usuario;
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

            // Mapear cuentas de utilidad
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

            await _context.SaveChangesAsync(ct);

            // Guardar líneas de resultado
            var lineasExistentes = await _context.con_configuracion_linea_resultados
                .Where(r => r.company_id == companyId)
                .ToListAsync(ct);

            _context.con_configuracion_linea_resultados.RemoveRange(lineasExistentes);

            if (dto.LineasResultado?.Count > 0)
            {
                var lineasNuevas = dto.LineasResultado
                    .Select((lr, i) => new con_configuracion_linea_resultado
                    {
                        company_id = companyId,
                        periodo_id = periodoActivo.PeriodoId,
                        numero_linea = (short)(i + 1),
                        tipo_linea = (byte)(lr.Tipo == "Ingreso" ? 0 : lr.Tipo == "Costo" ? 1 : 2),
                        codigo_cuenta = lr.CodigoCuenta,
                        descripcion_linea = lr.Descripcion,
                        nivel_indentacion = lr.NivelIndentacion,
                        mostrar_subtotal = lr.MostrarSubtotal,
                        created_by = usuario,
                        created_at = ahora
                    })
                    .ToList();

                _context.con_configuracion_linea_resultados.AddRange(lineasNuevas);
            }

            // Guardar líneas de balance
            var balancesExistentes = await _context.con_configuracion_balances
                .Where(b => b.company_id == companyId)
                .ToListAsync(ct);

            _context.con_configuracion_balances.RemoveRange(balancesExistentes);

            if (dto.LineasBalance?.Count > 0)
            {
                var balancesNuevos = dto.LineasBalance
                    .Select((lb, i) => new con_configuracion_balance
                    {
                        company_id = companyId,
                        periodo_id = periodoActivo.PeriodoId,
                        numero_linea = (short)(i + 1),
                        clase = lb.Clase,
                        codigo_cuenta = lb.CodigoCuenta,
                        descripcion_linea = lb.Descripcion,
                        descripcion_cuenta = lb.Descripcion, // Guardar también en descripcion_cuenta
                        porcentaje_activo = lb.PorcentajeActivo,
                        mostrar_en_reporte = lb.MostrarEnReporte,
                        created_by = usuario,
                        created_at = ahora
                    })
                    .ToList();

                _context.con_configuracion_balances.AddRange(balancesNuevos);
            }

            // Guardar correlativos
            var correlativosExistentes = await _context.con_configuracion_correlativos
                .Where(c => c.company_id == companyId)
                .ToListAsync(ct);

            _context.con_configuracion_correlativos.RemoveRange(correlativosExistentes);

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
                        created_by = usuario,
                        created_at = ahora
                    })
                    .ToList();

                _context.con_configuracion_correlativos.AddRange(correlativosNuevos);
            }

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);

            // Retornar configuración guardada
            var configuracionGuardada = await ObtenerAsync(companyId, ct);
            return configuracionGuardada ?? throw new InvalidOperationException(
                "No se pudo recuperar la configuración después de guardar.");
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> ExistePlanCuentasAsync(long companyId, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        return await _context.con_plan_cuentas
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId, ct);
    }

    public async Task<bool> ExistePeriodoAbiertoAsync(long companyId, CancellationToken ct = default)
    {
        return await _periodoService.ExistePeriodoAbiertoAsync(companyId, ct);
    }

    public async Task<ConfiguracionSistemaDto> InicializarConfiguracionPorDefectoAsync(long companyId,
        long tenantCompanyId, DateTime? fechaInicio = null, CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        // Crear período inicial si no existe
        await _periodoService.ObtenerOCrearPeriodoInicialAsync(companyId, fechaInicio, ct);

        var hoy = DateTime.Today;
        var fechaInicioPeriodo = fechaInicio ?? new DateTime(hoy.Year, 1, 1);

        // Crear DTO por defecto
        var config = new ConfiguracionSistemaDto
        {
            Principal = new ConfiguracionPrincipalDto
            {
                FechaInicioEjercicio = fechaInicioPeriodo,
                FechaFinEjercicio = new DateTime(fechaInicioPeriodo.Year, 12, 31),
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
        };

        // Guardar la configuración
        return await GuardarAsync(companyId, config, "system", ct);
    }

    private static ConfiguracionSistemaDto MapearDto(con_configuracion_sistema config,
        List<con_configuracion_linea_resultado> lineasResultado,
        List<con_configuracion_balance> lineasBalance,
        List<con_configuracion_correlativo> correlativos)
    {
        return new ConfiguracionSistemaDto
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
    }
}
