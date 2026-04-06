using Microsoft.EntityFrameworkCore;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación de ISaldosService
/// Maneja actualización de saldos en con_saldo_cuenta
/// Estructura: saldo_id, company_id, periodo_id, codigo_cuenta, mes (1-13), tipo_transaccion, debitos/creditos
/// </summary>
public sealed class SaldosService : ISaldosService
{
    private readonly SiadDbContext _context;

    public SaldosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task ActualizarSaldosPorPolizaAsync(
        long companyId,
        long polizaId,
        bool sumar,
        CancellationToken ct = default
    )
    {
        // Obtener póliza con todas sus líneas
        var poliza = await _context.con_partida_hdrs
            .Include(p => p.lineas)
            .ThenInclude(l => l.account)
            .Where(x => x.poliza_id == polizaId && x.company_id == companyId)
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Póliza {polizaId} no encontrada");

        if (poliza.period_id == null)
            throw new InvalidOperationException("La póliza debe tener período asociado para actualizar saldos");

        // Procesar cada línea
        if (poliza.lineas != null && poliza.lineas.Count > 0)
        {
            foreach (var linea in poliza.lineas)
            {
                var codigoCuenta = linea.account?.code ?? "";
                
                // Buscar o crear registro de saldo
                var saldo = await _context.con_saldo_cuentas
                    .Where(x =>
                        x.company_id == companyId &&
                        x.periodo_id == poliza.period_id.Value &&
                        x.codigo_cuenta == codigoCuenta
                    )
                    .FirstOrDefaultAsync(ct);

                if (saldo == null)
                {
                    // Crear nuevo registro de saldo
                    saldo = new con_saldo_cuenta
                    {
                        company_id = companyId,
                        periodo_id = poliza.period_id.Value,
                        codigo_cuenta = codigoCuenta,
                        mes = 13, // Acumulado
                        tipo_transaccion = 0,
                        debitos = 0,
                        creditos = 0,
                        cantidad_debitos = 0,
                        cantidad_creditos = 0,
                        presupuesto = 0,
                        created_at = DateTime.UtcNow
                    };
                    _context.con_saldo_cuentas.Add(saldo);
                }

                // Actualizar saldos
                if (sumar)
                {
                    // Registrar póliza
                    saldo.debitos += linea.debit_amount;
                    saldo.creditos += linea.credit_amount;
                    if (linea.debit_amount > 0) saldo.cantidad_debitos++;
                    if (linea.credit_amount > 0) saldo.cantidad_creditos++;
                }
                else
                {
                    // Revertir póliza
                    saldo.debitos -= linea.debit_amount;
                    saldo.creditos -= linea.credit_amount;
                    if (linea.debit_amount > 0) saldo.cantidad_debitos--;
                    if (linea.credit_amount > 0) saldo.cantidad_creditos--;
                }

                saldo.updated_at = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync(ct);
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

    public async Task InicializarPeriodoAsync(
        long companyId,
        long nuevoPeriodId,
        long periodoAnteriorId,
        CancellationToken ct = default
    )
    {
        // Obtener todos los saldos del período anterior
        var saldosAnteriores = await _context.con_saldo_cuentas
            .Where(x =>
                x.company_id == companyId &&
                x.periodo_id == periodoAnteriorId
            )
            .ToListAsync(ct);

        // Copiar como saldos iniciales del nuevo período
        foreach (var saldoAnterior in saldosAnteriores)
        {
            var nuevoSaldo = new con_saldo_cuenta
            {
                company_id = companyId,
                periodo_id = nuevoPeriodId,
                codigo_cuenta = saldoAnterior.codigo_cuenta,
                mes = 13,
                tipo_transaccion = 0,
                debitos = saldoAnterior.debitos, // Saldos del período anterior pasan como débitos
                creditos = saldoAnterior.creditos,
                cantidad_debitos = 0,
                cantidad_creditos = 0,
                presupuesto = 0,
                created_at = DateTime.UtcNow
            };

            _context.con_saldo_cuentas.Add(nuevoSaldo);
        }

        await _context.SaveChangesAsync(ct);
    }
}

