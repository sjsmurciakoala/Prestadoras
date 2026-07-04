using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación de ISaldosService — solo lectura sobre con_saldo_cuenta.
/// F6 retiró de aquí los métodos que escribían el caché desde C#
/// (ActualizarSaldosPorPolizaAsync / InicializarPeriodoAsync): nunca tuvieron
/// consumidores y duplicaban al motor único de BD, violando D1 si alguien
/// los hubiera llamado.
/// </summary>
public sealed class SaldosService : ISaldosService
{
    private readonly SiadDbContext _context;

    public SaldosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<(decimal debitos, decimal creditos)> ObtenerSaldoAsync(
        long companyId,
        long periodId,
        long accountId,
        long? costCenterId = null,
        CancellationToken ct = default
    )
    {
        // Obtener código de cuenta
        var cuenta = await _context.con_plan_cuentas
            .Where(x => x.account_id == accountId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Cuenta {accountId} no encontrada");

        var saldo = await _context.con_saldo_cuentas
            .Where(x =>
                x.company_id == companyId &&
                x.periodo_id == periodId &&
                x.codigo_cuenta == cuenta.code
            )
            .FirstOrDefaultAsync(ct);

        if (saldo == null)
            return (0, 0);

        return (saldo.debitos, saldo.creditos);
    }

    public async Task<SaldoVerificacionResultDto> VerificarAsync(
        long companyId,
        long? periodId = null,
        CancellationToken ct = default
    )
    {
        if (companyId <= 0)
        {
            throw new ArgumentException("La empresa es obligatoria.", nameof(companyId));
        }

        var conn = _context.Database.GetDbConnection();
        var filas = await conn.QueryAsync<(long period_id, string? periodo_code, string codigo_cuenta,
                string tipo_divergencia, decimal? debitos_cache, decimal? debitos_libro,
                decimal? creditos_cache, decimal? creditos_libro,
                int? cantidad_debitos_cache, int? cantidad_debitos_libro,
                int? cantidad_creditos_cache, int? cantidad_creditos_libro)>(
            new CommandDefinition(@"
                SELECT period_id, periodo_code, codigo_cuenta, tipo_divergencia,
                       debitos_cache, debitos_libro, creditos_cache, creditos_libro,
                       cantidad_debitos_cache, cantidad_debitos_libro,
                       cantidad_creditos_cache, cantidad_creditos_libro
                FROM public.fn_con_verificar_saldo_cuenta(@companyId, @periodId, NULL)",
                new { companyId, periodId },
                cancellationToken: ct));

        var divergencias = filas
            .Select(f => new SaldoDivergenciaDto
            {
                PeriodId = f.period_id,
                PeriodoCode = f.periodo_code,
                CodigoCuenta = f.codigo_cuenta,
                TipoDivergencia = f.tipo_divergencia,
                DebitosCache = f.debitos_cache,
                DebitosLibro = f.debitos_libro,
                CreditosCache = f.creditos_cache,
                CreditosLibro = f.creditos_libro,
                CantidadDebitosCache = f.cantidad_debitos_cache,
                CantidadDebitosLibro = f.cantidad_debitos_libro,
                CantidadCreditosCache = f.cantidad_creditos_cache,
                CantidadCreditosLibro = f.cantidad_creditos_libro
            })
            .ToList();

        return new SaldoVerificacionResultDto
        {
            VerificadoEn = DateTime.UtcNow,
            TotalDivergencias = divergencias.Count,
            SoloCache = divergencias.Count(d => d.TipoDivergencia == SaldoDivergenciaTipos.SoloCache),
            SoloLibro = divergencias.Count(d => d.TipoDivergencia == SaldoDivergenciaTipos.SoloLibro),
            Montos = divergencias.Count(d => d.TipoDivergencia == SaldoDivergenciaTipos.Montos),
            Conteos = divergencias.Count(d => d.TipoDivergencia == SaldoDivergenciaTipos.Conteos),
            FechasFueraPeriodo = divergencias.Count(d => d.TipoDivergencia == SaldoDivergenciaTipos.FechaFueraPeriodo),
            DetalleTruncado = divergencias.Count > MaxDetalleDivergencias,
            // Tras una remigración rota el detalle puede ser períodos×cuentas
            // (decenas de miles de filas): los contadores van completos, el
            // detalle al browser se acota.
            Divergencias = divergencias.Count > MaxDetalleDivergencias
                ? divergencias.Take(MaxDetalleDivergencias).ToList()
                : divergencias
        };
    }

    private const int MaxDetalleDivergencias = 500;
}
