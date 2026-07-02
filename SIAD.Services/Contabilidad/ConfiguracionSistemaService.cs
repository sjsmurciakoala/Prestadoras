using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementacion del servicio de configuracion del sistema contable.
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

        return await ObtenerAsyncInternal(companyId, ignoreTenantScope: false, ct);
    }

    public async Task<ConfiguracionSistemaDto> GuardarAsync(long companyId, ConfiguracionSistemaDto dto, string usuario,
        CancellationToken ct = default)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }

        ArgumentNullException.ThrowIfNull(dto);
        return await GuardarAsyncInternal(companyId, dto, usuario, ignoreTenantScope: false, ct);
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

        _ = tenantCompanyId;

        var hoy = DateTime.Today;
        var fechaInicioPeriodo = fechaInicio ?? new DateTime(hoy.Year, 1, 1);

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

        // Durante el bootstrap la empresa nueva aun no es el tenant activo del contexto.
        return await GuardarAsyncInternal(companyId, config, "system", ignoreTenantScope: true, ct);
    }

    private async Task<ConfiguracionSistemaDto?> ObtenerAsyncInternal(long companyId, bool ignoreTenantScope,
        CancellationToken ct)
    {
        var configuracionQuery = ignoreTenantScope
            ? _context.con_configuracion_sistemas.IgnoreQueryFilters()
            : _context.con_configuracion_sistemas;
        var lineasResultadoQuery = ignoreTenantScope
            ? _context.con_configuracion_linea_resultados.IgnoreQueryFilters()
            : _context.con_configuracion_linea_resultados;
        var lineasBalanceQuery = ignoreTenantScope
            ? _context.con_configuracion_balances.IgnoreQueryFilters()
            : _context.con_configuracion_balances;
        var correlativosQuery = ignoreTenantScope
            ? _context.con_configuracion_correlativos.IgnoreQueryFilters()
            : _context.con_configuracion_correlativos;

        var config = await configuracionQuery
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        if (config is null)
        {
            return null;
        }

        var lineasResultado = await lineasResultadoQuery
            .AsNoTracking()
            .Where(r => r.company_id == companyId)
            .OrderBy(r => r.numero_linea)
            .ToListAsync(ct);

        var lineasBalance = await lineasBalanceQuery
            .AsNoTracking()
            .Where(b => b.company_id == companyId)
            .OrderBy(b => b.numero_linea)
            .ToListAsync(ct);

        var correlativos = await correlativosQuery
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .ToListAsync(ct);

        return MapearDto(config, lineasResultado, lineasBalance, correlativos);
    }

    private async Task<ConfiguracionSistemaDto> GuardarAsyncInternal(long companyId, ConfiguracionSistemaDto dto,
        string usuario, bool ignoreTenantScope, CancellationToken ct)
    {
        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

        var configuracionQuery = ignoreTenantScope
            ? _context.con_configuracion_sistemas.IgnoreQueryFilters()
            : _context.con_configuracion_sistemas;
        var lineasResultadoQuery = ignoreTenantScope
            ? _context.con_configuracion_linea_resultados.IgnoreQueryFilters()
            : _context.con_configuracion_linea_resultados;
        var lineasBalanceQuery = ignoreTenantScope
            ? _context.con_configuracion_balances.IgnoreQueryFilters()
            : _context.con_configuracion_balances;
        var correlativosQuery = ignoreTenantScope
            ? _context.con_configuracion_correlativos.IgnoreQueryFilters()
            : _context.con_configuracion_correlativos;

        var ahora = DateTime.UtcNow;
        IDbContextTransaction? transaction = _context.Database.CurrentTransaction;
        var ownsTransaction = transaction is null;

        if (ownsTransaction)
        {
            transaction = await _context.Database.BeginTransactionAsync(ct);
        }

        try
        {
            var principal = dto.Principal
                ?? throw new InvalidOperationException("La configuracion principal es obligatoria.");
            var fechaInicio = principal.FechaInicioEjercicio;
            var periodoActivo = await _periodoService.ObtenerOCrearPeriodoInicialAsync(companyId, fechaInicio, ct);

            var config = await configuracionQuery
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

            config.fecha_inicio_ejercicio = principal.FechaInicioEjercicio;
            config.fecha_fin_ejercicio = principal.FechaFinEjercicio;
            config.meses_calculados = principal.MesesCalculados;
            config.separador_codigo = principal.SeparadorCodigo;
            config.formato_cuentas = principal.FormatoCuentas;
            config.formato_centros = principal.FormatoCentros;
            config.symbol_saldo_acreedor = principal.SymbolSaldoAcreedor;
            config.monto_maximo = principal.MontoMaximo;
            config.frecuencia_depreciacion = principal.FrecuenciaDepreciacion;
            config.ultima_depreciacion = principal.UltimaDepreciacion;

            config.codigo_cuenta_util_acumulada_historica = dto.CuentasUtilidad.CodigoCuentaUtilAcumuladaHistorica;
            config.codigo_cuenta_util_acumulada_inflacion = dto.CuentasUtilidad.CodigoCuentaUtilAcumuladaInflacion;
            config.codigo_cuenta_util_ejercicio_historica = dto.CuentasUtilidad.CodigoCuentaUtilEjercicioHistorica;
            config.codigo_cuenta_util_ejercicio_inflacion = dto.CuentasUtilidad.CodigoCuentaUtilEjercicioInflacion;
            config.codigo_cuenta_perdida_acumulada_historica = dto.CuentasUtilidad.CodigoCuentaPerdidaAcumuladaHistorica;
            config.codigo_cuenta_perdida_acumulada_inflacion = dto.CuentasUtilidad.CodigoCuentaPerdidaAcumuladaInflacion;
            config.codigo_cuenta_perdida_ejercicio_historica = dto.CuentasUtilidad.CodigoCuentaPerdidaEjercicioHistorica;
            config.codigo_cuenta_perdida_ejercicio_inflacion = dto.CuentasUtilidad.CodigoCuentaPerdidaEjercicioInflacion;

            config.mostrar_orden = dto.EstadoSituacionFinanciera.MostrarOrden;
            config.mostrar_percontra = dto.EstadoSituacionFinanciera.MostrarPercontra;

            await _context.SaveChangesAsync(ct);

            var lineasExistentes = await lineasResultadoQuery
                .Where(r => r.company_id == companyId)
                .ToListAsync(ct);
            _context.con_configuracion_linea_resultados.RemoveRange(lineasExistentes);

            if (dto.LineasResultado?.Count > 0)
            {
                var lineasNuevas = dto.LineasResultado
                    .Select((lr, i) => new con_configuracion_linea_resultado
                    {
                        company_id = companyId,
                        periodo_id = periodoActivo.PeriodId,
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

            var balancesExistentes = await lineasBalanceQuery
                .Where(b => b.company_id == companyId)
                .ToListAsync(ct);
            _context.con_configuracion_balances.RemoveRange(balancesExistentes);

            if (dto.LineasBalance?.Count > 0)
            {
                var balancesNuevos = dto.LineasBalance
                    .Select((lb, i) => new con_configuracion_balance
                    {
                        company_id = companyId,
                        periodo_id = periodoActivo.PeriodId,
                        numero_linea = (short)(i + 1),
                        clase = lb.Clase,
                        codigo_cuenta = lb.CodigoCuenta,
                        descripcion_linea = lb.Descripcion,
                        descripcion_cuenta = lb.Descripcion,
                        porcentaje_activo = lb.PorcentajeActivo,
                        mostrar_en_reporte = lb.MostrarEnReporte,
                        created_by = usuario,
                        created_at = ahora
                    })
                    .ToList();

                _context.con_configuracion_balances.AddRange(balancesNuevos);
            }

            var correlativosExistentes = await correlativosQuery
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

            if (ownsTransaction && transaction is not null)
            {
                await transaction.CommitAsync(ct);
            }

            var configuracionGuardada = await ObtenerAsyncInternal(companyId, ignoreTenantScope, ct);
            return configuracionGuardada ?? throw new InvalidOperationException(
                "No se pudo recuperar la configuracion despues de guardar.");
        }
        catch
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.RollbackAsync(ct);
            }

            throw;
        }
        finally
        {
            if (ownsTransaction && transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    private static ConfiguracionSistemaDto MapearDto(con_configuracion_sistema config,
        List<con_configuracion_linea_resultado> lineasResultado,
        List<con_configuracion_balance> lineasBalance,
        List<con_configuracion_correlativo> correlativos)
    {
        var estadoSituacionFinanciera = MapearEstadoSituacionFinanciera(config, lineasBalance);

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
            EstadoSituacionFinanciera = estadoSituacionFinanciera,
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

    private static EstadoSituacionFinancieraDto MapearEstadoSituacionFinanciera(
        con_configuracion_sistema config,
        IReadOnlyList<con_configuracion_balance> lineasBalance)
    {
        var codigosPorClase = lineasBalance
            .OrderBy(lb => lb.numero_linea)
            .GroupBy(lb => lb.clase)
            .ToDictionary(
                group => group.Key,
                group => group
                    .Select(lb => string.IsNullOrWhiteSpace(lb.codigo_cuenta) ? null : lb.codigo_cuenta.Trim())
                    .Where(codigo => !string.IsNullOrWhiteSpace(codigo))
                    .Cast<string>()
                    .ToList());

        string? ObtenerCodigo(byte clase, int indice)
        {
            if (!codigosPorClase.TryGetValue(clase, out var codigos) || indice < 0 || indice >= codigos.Count)
            {
                return null;
            }

            return codigos[indice];
        }

        var codigoOrden = ObtenerCodigo(7, 0);
        var codigoPercontra = ObtenerCodigo(8, 0);

        return new EstadoSituacionFinancieraDto
        {
            CodigoActivoCortoPlazo1 = ObtenerCodigo(1, 0),
            CodigoActivoCortoPlazo2 = ObtenerCodigo(1, 1),
            CodigoActivoLargoPlazo1 = ObtenerCodigo(2, 0),
            CodigoActivoLargoPlazo2 = ObtenerCodigo(2, 1),
            CodigoPasivoCortoPlazo1 = ObtenerCodigo(3, 0),
            CodigoPasivoCortoPlazo2 = ObtenerCodigo(3, 1),
            CodigoPasivoLargoPlazo1 = ObtenerCodigo(4, 0),
            CodigoPasivoLargoPlazo2 = ObtenerCodigo(4, 1),
            CodigoCapitalAportado = ObtenerCodigo(5, 0),
            CodigoResultadosAcumulados = ObtenerCodigo(5, 1),
            CodigoUtilidadPerdidaEjercicio = ObtenerCodigo(5, 2),
            CodigoSobrevaluaciones = ObtenerCodigo(5, 3),
            CodigoPasivoyCapital = ObtenerCodigo(6, 0),
            MostrarOrden = config.mostrar_orden || !string.IsNullOrWhiteSpace(codigoOrden),
            MostrarPercontra = config.mostrar_percontra || !string.IsNullOrWhiteSpace(codigoPercontra)
        };
    }
}
