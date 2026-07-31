using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Cobros;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Bancos;
using SIAD.Services.Cobranza;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Cobros;

/// <summary>
/// Motor único de cobro (unificación cobranza F2 — plan §4). Absorbe la lógica
/// compartida de los caminos legacy (captación 201, abono 202): aplicación FIFO
/// por documento, estados de factura A→B→C, contabilidad por configuración,
/// kardex bancario, cancelación de cortes, y el modelo nuevo adm_pago +
/// adm_pago_aplicacion con folio por empresa. Dual-write hacia
/// transaccion_abonado hasta F7 para que arqueos, reportes y asientos sigan
/// cuadrando durante la transición.
/// </summary>
public class CobroService : ICobroService
{
    private readonly SiadDbContext _context;
    private readonly IBanTransaccionesService _banTransaccionesService;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly ICorteMasivoService _corteMasivoService;

    private const string BancoMarkerPrefix = "BANCO_CUENTA:";
    private const string TipoTransaccionBancoDeposito = "DEP";

    public CobroService(
        SiadDbContext context,
        IBanTransaccionesService banTransaccionesService,
        ICurrentCompanyService currentCompanyService,
        ICorteMasivoService corteMasivoService)
    {
        _context = context;
        _banTransaccionesService = banTransaccionesService;
        _currentCompanyService = currentCompanyService;
        _corteMasivoService = corteMasivoService;
    }

    public async Task<ResponseModelDto> RegistrarCobroAsync(CobroCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
            return ResponseModelDto.Fail("Debe indicar el cliente.");
        if (dto.Aplicaciones is null || dto.Aplicaciones.Count == 0)
            return ResponseModelDto.Fail("El cobro debe aplicar al menos a un documento.");
        if (dto.Aplicaciones.Any(a => a.Monto <= 0))
            return ResponseModelDto.Fail("Todos los montos aplicados deben ser mayores a cero.");
        if (dto.Aplicaciones.Any(a => a.DocumentoTipo != DocumentoCobroTipo.Factura
                                       && a.DocumentoTipo != DocumentoCobroTipo.CuotaPlan
                                       && a.DocumentoTipo != DocumentoCobroTipo.NotaDebito))
            return ResponseModelDto.Fail("Tipo de documento no soportado (factura, cuota de plan o nota de débito).");
        if (dto.Aplicaciones.Any(a => a.DocumentoTipo == DocumentoCobroTipo.Factura && !a.FacturaId.HasValue))
            return ResponseModelDto.Fail("Cada aplicación a factura debe indicar la factura.");
        if (dto.Aplicaciones.Any(a => a.DocumentoTipo == DocumentoCobroTipo.CuotaPlan && !a.PlanCuotaId.HasValue))
            return ResponseModelDto.Fail("Cada aplicación a cuota debe indicar la cuota del plan.");
        if (dto.Aplicaciones.Any(a => a.DocumentoTipo == DocumentoCobroTipo.NotaDebito && !a.NotaDebitoId.HasValue))
            return ResponseModelDto.Fail("Cada aplicación a nota de débito debe indicar la nota.");
        if (dto.FormaPago != "EFECTIVO" && dto.FormaPago != "BANCO")
            return ResponseModelDto.Fail("Forma de pago inválida (EFECTIVO o BANCO).");

        var facturaIds = dto.Aplicaciones
            .Where(a => a.DocumentoTipo == DocumentoCobroTipo.Factura)
            .Select(a => a.FacturaId!.Value).ToList();
        if (facturaIds.Distinct().Count() != facturaIds.Count)
            return ResponseModelDto.Fail("Hay aplicaciones duplicadas a la misma factura.");
        var cuotaIds = dto.Aplicaciones
            .Where(a => a.DocumentoTipo == DocumentoCobroTipo.CuotaPlan)
            .Select(a => a.PlanCuotaId!.Value).ToList();
        if (cuotaIds.Distinct().Count() != cuotaIds.Count)
            return ResponseModelDto.Fail("Hay aplicaciones duplicadas a la misma cuota.");
        var notaIds = dto.Aplicaciones
            .Where(a => a.DocumentoTipo == DocumentoCobroTipo.NotaDebito)
            .Select(a => a.NotaDebitoId!.Value).ToList();
        if (notaIds.Distinct().Count() != notaIds.Count)
            return ResponseModelDto.Fail("Hay aplicaciones duplicadas a la misma nota de débito.");

        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        // Fecha LOCAL del servidor (Honduras): el "día" de la caja es el día
        // operativo, no el UTC (con UTC un cobro de las 6pm caía en mañana).
        var fechaPago = dto.FechaPago?.Date ?? DateTime.Now.Date;
        var fechaHoy = DateOnly.FromDateTime(fechaPago);
        var montoTotal = dto.Aplicaciones.Sum(a => a.Monto);
        var referencia = string.IsNullOrWhiteSpace(dto.ReferenciaExterna) ? null : dto.ReferenciaExterna.Trim();

        // ---------- Regla única (plan §4): canal caja exige sesión ABIERTA ----------
        int? sesionCajaId = null;
        if (dto.Canal == CanalCobro.Caja)
        {
            sesionCajaId = await _context.sesion_cajas
                .AsNoTracking()
                .Where(s => s.company_id == companyId && s.usuario_apertura == usuario && s.estado == "ABIERTA")
                .Select(s => (int?)s.id)
                .FirstOrDefaultAsync(ct);
            if (sesionCajaId is null)
                return ResponseModelDto.Fail(
                    $"El usuario {usuario} no tiene una sesión de caja abierta; abra la sesión antes de cobrar.");
        }

        // ---------- Idempotencia — chequeo rápido pre-tx (barato, sin lock) ----------
        if (referencia is not null)
        {
            var previo = await BuscarCobroPorReferenciaAsync(companyId, referencia, ct);
            if (previo is not null) return previo;
        }

        var periodo = await ObtenerPeriodoActualCodigoAsync(ct);
        var integrarBancos = dto.FormaPago == "BANCO" && dto.BancoCuentaId is > 0;
        var banco = await ResolverBancoCodigoAsync(dto.BancoCuentaId, dto.Banco, ct);

        if (dto.ReciboPendienteId.HasValue)
        {
            // F7: ReciboPendienteId es adm_recibo_banco_pendiente.recibo_pendiente_id.
            var existePendiente = await _context.adm_recibo_banco_pendientes
                .AnyAsync(r => r.recibo_pendiente_id == dto.ReciboPendienteId.Value && r.estado_id == 2, ct);
            if (!existePendiente)
                return ResponseModelDto.Fail("El recibo pendiente indicado no existe o ya fue procesado.");
        }

        var clienteInfo = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == dto.ClienteClave)
            .Select(c => new
            {
                c.ciclos_id,
                c.maestro_cliente_indicativo_ruta,
                c.maestro_cliente_secuencia,
                c.maestro_cliente_tiene_medidor,
                c.bloqueado_cobranza
            })
            .FirstOrDefaultAsync(ct);

        // Pruebas operativas jul-2026: un cliente bloqueado por cobranza no
        // puede cobrar por NINGÚN canal hasta que Cobranza lo desbloquee.
        if (clienteInfo?.bloqueado_cobranza == true)
        {
            return ResponseModelDto.Fail(
                "El cliente está BLOQUEADO por cobranza. Gestione el desbloqueo en Gestión de Cobranza antes de cobrar.");
        }

        // ---------- Plan pre-tx (solo lectura): validación y derrame proyectado ----------
        // Base del kardex bancario (que postea su propia transacción, patrón legacy)
        // y validación temprana. El derrame definitivo se recalcula DENTRO de la
        // transacción con las filas bloqueadas — MISMA estrategia (porcentajes).
        var porcentajes = await CargarPorcentajesDesgloseAsync(
            _context.Database.GetDbConnection(), companyId,
            _context.Database.CurrentTransaction?.GetDbTransaction(), ct);

        var planContable = new List<(string? ServicioCodigo, decimal Monto)>();
        int? categoriaServicioId = null;
        bool? conMedicion = null;
        string? numFacturaPrincipal = null;
        var numReciboPrincipal = 0;
        foreach (var apl in dto.Aplicaciones)
        {
            if (apl.DocumentoTipo == DocumentoCobroTipo.NotaDebito)
            {
                // F7 H2b: validación temprana de la ND (bloqueo definitivo en-tx).
                var ndVista = await _context.adm_nota_debitos
                    .AsNoTracking()
                    .Where(n => n.nota_debito_id == apl.NotaDebitoId!.Value && n.company_id == companyId)
                    .Select(n => new { n.nota_debito_id, n.estado_id, n.saldo_pendiente })
                    .FirstOrDefaultAsync(ct);
                if (ndVista is null)
                    return ResponseModelDto.Fail($"No se encontró la nota de débito {apl.NotaDebitoId}.");
                if (ndVista.estado_id == 3 || ndVista.saldo_pendiente <= 0)
                    return ResponseModelDto.Fail("La nota de débito ya está cobrada o anulada.");
                if (apl.Monto > ndVista.saldo_pendiente)
                    return ResponseModelDto.Fail(
                        $"El monto aplicado ({apl.Monto:N2}) excede el saldo de la nota ({ndVista.saldo_pendiente:N2}).");

                planContable.AddRange(await DistribuirNdContableAsync(apl.NotaDebitoId!.Value, apl.Monto, ct));
                continue;
            }

            if (apl.DocumentoTipo == DocumentoCobroTipo.CuotaPlan)
            {
                // Validación temprana de la cuota (el bloqueo definitivo es en-tx).
                var cuotaVista = await _context.cln_plan_pago_dtls
                    .AsNoTracking()
                    .Where(d => d.id == apl.PlanCuotaId!.Value)
                    .Select(d => new { d.id, d.estado_id, d.saldo_cuota, d.idhdr })
                    .FirstOrDefaultAsync(ct);
                if (cuotaVista is null)
                    return ResponseModelDto.Fail($"No se encontró la cuota {apl.PlanCuotaId}.");
                if (cuotaVista.estado_id is not (EstadoDocumentoComercial.Activa or EstadoDocumentoComercial.ParcialmenteAbonada))
                    return ResponseModelDto.Fail("La cuota ya está pagada o anulada.");
                if (apl.Monto > cuotaVista.saldo_cuota)
                    return ResponseModelDto.Fail(
                        $"El monto aplicado ({apl.Monto:N2}) excede el saldo de la cuota ({cuotaVista.saldo_cuota:N2}).");

                var planActivo = await _context.cln_plan_pago_hdrs
                    .AsNoTracking()
                    .AnyAsync(h => h.id == cuotaVista.idhdr && h.estado_id == EstadoPlan.Activo, ct);
                if (!planActivo)
                    return ResponseModelDto.Fail("El plan de pago de la cuota no está activo.");

                // Parte contable: desglose porcentual configurado (aproximación
                // del pago de convenio entre servicios, como el posteo legacy);
                // sin configuración va a la CxC general.
                planContable.AddRange(DistribuirCuotaContable(apl.Monto, porcentajes));
                continue;
            }

            var vista = await _context.facturas
                .AsNoTracking()
                .Where(f => f.company_id == companyId && f.id == apl.FacturaId!.Value)
                .Select(f => new { f.id, f.estado, f.numfactura, f.numrecibo, f.saldototal, f.categoria_servicio_id, f.con_medicion })
                .FirstOrDefaultAsync(ct);
            if (vista is null)
                return ResponseModelDto.Fail($"No se encontró la factura {apl.FacturaId}.");
            if (vista.estado == "N")
                return ResponseModelDto.Fail($"La factura {vista.numfactura ?? vista.numrecibo.ToString()} está anulada.");

            if (numFacturaPrincipal is null)
            {
                numFacturaPrincipal = vista.numfactura;
                numReciboPrincipal = vista.numrecibo;
                categoriaServicioId = vista.categoria_servicio_id;
                conMedicion = vista.con_medicion;
            }

            // Clones desconectados para proyectar el derrame sin tocar nada.
            var lineas = await _context.factura_detalles
                .AsNoTracking()
                .Where(d => d.factura_id == vista.id)
                .OrderBy(d => d.id)
                .Select(d => new factura_detalle
                {
                    id = d.id,
                    montovalor_saldo = d.montovalor_saldo,
                    montovalor = d.montovalor,
                    tiposervicio = d.tiposervicio,
                    codigo = d.codigo
                })
                .ToListAsync(ct);

            var saldoDetalles = lineas.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m);
            var saldoPendiente = saldoDetalles > 0 ? saldoDetalles : (vista.saldototal ?? 0m);
            if (saldoPendiente <= 0)
                return ResponseModelDto.Fail(
                    $"La factura {vista.numfactura ?? vista.numrecibo.ToString()} no tiene saldo pendiente.");
            if (apl.Monto > saldoPendiente)
                return ResponseModelDto.Fail(
                    $"El monto aplicado ({apl.Monto:N2}) excede el saldo pendiente ({saldoPendiente:N2}) de la factura {vista.numfactura ?? vista.numrecibo.ToString()}.");

            var lineasPlan = new List<(int, int?, decimal)>();
            var restantePlan = AplicarMontoALineas(vista.id, lineas, apl.Monto, porcentajes, lineasPlan, planContable);
            if (restantePlan > 0)
                planContable.Add((null, restantePlan));
        }

        // Recibos misceláneos legacy: las líneas contables van contra la CxC
        // GENERAL (servicio null), no la analítica — la aplicación por línea a
        // los documentos (adm_pago_aplicacion) no cambia.
        if (dto.CxcGeneral)
        {
            planContable = [(null, montoTotal)];
        }

        var documentoContable = ResolverDocumentoContable(dto.TipoLegacy);
        var referenciaContable = string.IsNullOrWhiteSpace(numFacturaPrincipal)
            ? $"{documentoContable.Documento}-{numReciboPrincipal}"
            : numFacturaPrincipal.Trim();
        var descripcionContable = $"Cobro factura {referenciaContable}";

        // ---------- Kardex bancario (pre-tx: BanTransaccionesService postea aparte) ----------
        (long BanKardexId, decimal SaldoResultante)? movimientoBanco = null;
        if (integrarBancos)
        {
            var periodoAbiertoId = await _context.con_periodo_contables
                .AsNoTracking()
                .Where(p => p.company_id == companyId
                    && p.status_id == 0
                    && p.start_date <= fechaPago
                    && p.end_date >= fechaPago)
                .Select(p => (long?)p.period_id)
                .FirstOrDefaultAsync(ct);
            if (periodoAbiertoId is null)
                return ResponseModelDto.Fail(
                    $"No hay período contable abierto para la fecha {fechaHoy:dd/MM/yyyy}; los cobros por banco requieren período abierto.");

            try
            {
                var contraCuentas = await IntegracionContableConfigSql.ConstruirContraCuentasCxcAsync(
                    _context.Database.GetDbConnection(),
                    companyId,
                    planContable,
                    categoriaServicioId,
                    conMedicion,
                    descripcionContable,
                    referenciaContable,
                    ct);

                movimientoBanco = await _banTransaccionesService.RegistrarMovimientoAsync(
                    dto.BancoCuentaId!.Value,
                    TipoTransaccionBancoDeposito,
                    fechaHoy,
                    descripcionContable,
                    referenciaContable,
                    referenciaContable,
                    1m,
                    montoTotal,
                    contraCuentas,
                    usuario,
                    ct);
            }
            catch (Exception ex)
            {
                return ResponseModelDto.Fail($"Error al registrar movimiento bancario: {ex.Message}");
            }
        }

        // Transacción propia solo si no hay una ambiente (tests con rollback y
        // fachadas que ya traen transacción abierta la reutilizan).
        var ownsTx = _context.Database.CurrentTransaction is null;
        await using var tx = ownsTx ? await _context.Database.BeginTransactionAsync(ct) : null;
        var connection = _context.Database.GetDbConnection();
        var dbTransaction = _context.Database.CurrentTransaction!.GetDbTransaction();
        try
        {
            // ---------- Idempotencia definitiva (advisory lock + re-chequeo) ----------
            if (referencia is not null)
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    "SELECT pg_advisory_xact_lock(hashtextextended(@Key, 0))",
                    new { Key = $"cobro:{companyId}:{referencia}" },
                    dbTransaction, cancellationToken: ct));

                var previo = await BuscarCobroPorReferenciaAsync(companyId, referencia, ct);
                if (previo is not null)
                {
                    if (tx is not null) await tx.RollbackAsync(ct);
                    if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
                        await TryCompensarMovimientoBancarioAsync(dto.BancoCuentaId.Value, movimientoBanco.Value.BanKardexId, usuario, ct);
                    return previo;
                }
            }

            // ---------- Bloqueo de filas + derrame definitivo ----------
            var aplicacionesContables = new List<(string? ServicioCodigo, decimal Monto)>();
            var lineasAplicadas = new List<(int FacturaId, int? DetalleId, decimal Monto)>();
            var cuotasAplicadas = new List<(int PlanCuotaId, decimal Monto)>();
            var ndsAplicadas = new List<(long NotaDebitoId, decimal Monto)>();
            var planesTocados = new HashSet<int>();
            var resultados = new List<CobroAplicacionResultadoDto>();
            factura? facturaPrincipal = null;

            foreach (var apl in dto.Aplicaciones)
            {
                if (apl.DocumentoTipo == DocumentoCobroTipo.NotaDebito)
                {
                    // F7 H2b: la ND es un documento — bloquear y rebajar su
                    // saldo vivo. El estado FISCAL no cambia por cobros.
                    var nd = await _context.adm_nota_debitos
                        .FromSqlInterpolated($"SELECT * FROM public.adm_nota_debito WHERE company_id = {companyId} AND nota_debito_id = {apl.NotaDebitoId!.Value} FOR UPDATE")
                        .FirstOrDefaultAsync(ct);
                    if (nd is null)
                        return ResponseModelDto.Fail($"No se encontró la nota de débito {apl.NotaDebitoId}.");
                    if (nd.estado_id == 3 || apl.Monto > nd.saldo_pendiente)
                        return ResponseModelDto.Fail("El saldo de la nota de débito cambió; vuelva a consultar e intente de nuevo.");

                    nd.saldo_pendiente -= apl.Monto;
                    ndsAplicadas.Add((nd.nota_debito_id, apl.Monto));

                    resultados.Add(new CobroAplicacionResultadoDto
                    {
                        FacturaId = 0,
                        NumFactura = $"ND {nd.numero_documento}",
                        NumRecibo = 0,
                        MontoAplicado = apl.Monto,
                        SaldoRestante = nd.saldo_pendiente,
                        EstadoFactura = nd.saldo_pendiente <= 0m ? "C" : "B"
                    });
                    continue;
                }

                if (apl.DocumentoTipo == DocumentoCobroTipo.CuotaPlan)
                {
                    // F6: la cuota es un documento — bloquear, rebajar saldo y
                    // avanzar su estado (mismo catálogo que la factura).
                    var cuota = await _context.cln_plan_pago_dtls
                        .FromSqlInterpolated($"SELECT * FROM public.cln_plan_pago_dtl WHERE company_id = {companyId} AND id = {apl.PlanCuotaId!.Value} FOR UPDATE")
                        .FirstOrDefaultAsync(ct);
                    if (cuota is null)
                        return ResponseModelDto.Fail($"No se encontró la cuota {apl.PlanCuotaId}.");
                    if (cuota.estado_id is not (EstadoDocumentoComercial.Activa or EstadoDocumentoComercial.ParcialmenteAbonada)
                        || apl.Monto > cuota.saldo_cuota)
                        return ResponseModelDto.Fail("El saldo de la cuota cambió; vuelva a consultar e intente de nuevo.");

                    cuota.saldo_cuota -= apl.Monto;
                    cuota.estado_id = cuota.saldo_cuota <= 0m
                        ? EstadoDocumentoComercial.Cobrada
                        : EstadoDocumentoComercial.ParcialmenteAbonada;
                    cuota.estadopago = cuota.saldo_cuota <= 0m ? "Pagado" : "Pendiente"; // legacy hasta F7
                    cuota.usuariomodificacion = usuario;
                    cuota.fechamodificacion = DateTime.Now;

                    cuotasAplicadas.Add((cuota.id, apl.Monto));
                    if (cuota.idhdr.HasValue) planesTocados.Add(cuota.idhdr.Value);

                    resultados.Add(new CobroAplicacionResultadoDto
                    {
                        FacturaId = 0,
                        NumFactura = $"CUOTA-{cuota.mes}",
                        NumRecibo = 0,
                        MontoAplicado = apl.Monto,
                        SaldoRestante = cuota.saldo_cuota,
                        EstadoFactura = cuota.saldo_cuota <= 0m ? "C" : "B"
                    });
                    continue;
                }

                var factura = await _context.facturas
                    .FromSqlInterpolated($"SELECT * FROM public.factura WHERE company_id = {companyId} AND id = {apl.FacturaId!.Value} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
                if (factura is null)
                    return ResponseModelDto.Fail($"No se encontró la factura {apl.FacturaId}.");
                facturaPrincipal ??= factura;

                var detalles = await _context.factura_detalles
                    .Where(d => d.factura_id == factura.id)
                    .OrderBy(d => d.id)
                    .ToListAsync(ct);

                var saldoDetalles = detalles.Sum(d => d.montovalor_saldo ?? d.montovalor ?? 0m);
                var saldoPendiente = saldoDetalles > 0 ? saldoDetalles : (factura.saldototal ?? 0m);
                if (saldoPendiente <= 0 || apl.Monto > saldoPendiente)
                    return ResponseModelDto.Fail(
                        $"El saldo de la factura {factura.numfactura ?? factura.numrecibo.ToString()} cambió; vuelva a consultar e intente de nuevo.");

                // Derrame definitivo: prioridad a otros cargos + distribución por
                // porcentajes configurados (o FIFO si no hay configuración).
                var restante = AplicarMontoALineas(
                    factura.id, detalles, apl.Monto, porcentajes, lineasAplicadas, aplicacionesContables);

                if (restante > 0)
                {
                    lineasAplicadas.Add((factura.id, null, restante));
                    aplicacionesContables.Add((null, restante));
                }

                var nuevoSaldoFactura = saldoPendiente - apl.Monto;
                if (nuevoSaldoFactura <= 0)
                {
                    factura.estado = "C";
                    factura.fechapago = fechaHoy;
                    factura.usuario = usuario;
                    factura.recolectora = banco;
                }
                else
                {
                    factura.estado = "B";
                }

                resultados.Add(new CobroAplicacionResultadoDto
                {
                    FacturaId = factura.id,
                    NumFactura = factura.numfactura ?? factura.numrecibo.ToString(),
                    NumRecibo = factura.numrecibo,
                    MontoAplicado = apl.Monto,
                    SaldoRestante = nuevoSaldoFactura,
                    EstadoFactura = factura.estado!
                });
            }

            if (dto.CxcGeneral)
            {
                // La contabilidad va a la CxC general; el detalle por línea de
                // adm_pago_aplicacion se conserva intacto.
                aplicacionesContables = [(null, montoTotal)];
            }
            else
            {
                // F6: parte contable de las cuotas cobradas (desglose porcentual
                // configurado o CxC general) — espejo del planContable pre-tx.
                foreach (var (_, monto) in cuotasAplicadas)
                {
                    aplicacionesContables.AddRange(DistribuirCuotaContable(monto, porcentajes));
                }
                // F7 H2b: parte contable de las ND por sus propias líneas.
                foreach (var (notaDebitoId, monto) in ndsAplicadas)
                {
                    aplicacionesContables.AddRange(await DistribuirNdContableAsync(notaDebitoId, monto, ct));
                }
            }

            // F6: si el pago saldó la última cuota viva, el plan se COMPLETA.
            if (planesTocados.Count > 0)
            {
                // Persistir las cuotas rebajadas ANTES de consultar si quedan
                // vivas (la verificación va a la BD, no al change tracker).
                await _context.SaveChangesAsync(ct);
            }
            foreach (var planId in planesTocados)
            {
                var quedanVivas = await _context.cln_plan_pago_dtls
                    .AnyAsync(d => d.idhdr == planId
                                   && (d.estado_id == EstadoDocumentoComercial.Activa
                                       || d.estado_id == EstadoDocumentoComercial.ParcialmenteAbonada), ct);
                if (!quedanVivas)
                {
                    var plan = await _context.cln_plan_pago_hdrs.FirstOrDefaultAsync(h => h.id == planId, ct);
                    if (plan is not null)
                    {
                        plan.estado_id = EstadoPlan.Completado;
                        plan.estadopago = "Completado"; // legacy hasta F7
                        plan.usuariomodificacion = usuario;
                        plan.fechamodificacion = DateTime.Now;
                    }
                }
            }

            // ---------- F7 H2c: SE ACABÓ EL ESPEJO LEGACY ----------
            // El cobro vive solo como documento del motor (adm_pago +
            // aplicaciones). transaccion_abonado ya no se escribe: queda
            // congelada como archivo histórico (freeze en H4).
            var saldoActualCliente = await ObtenerSaldoClienteAsync(dto.ClienteClave, ct);

            // El papel "para banco" que se está cobrando debe seguir vivo
            // (adm_recibo_banco_pendiente); se marca APLICADO más abajo, con el
            // pago ya creado.
            if (dto.ReciboPendienteId.HasValue)
            {
                var pendienteVivo = await _context.adm_recibo_banco_pendientes
                    .AsNoTracking()
                    .AnyAsync(r => r.recibo_pendiente_id == dto.ReciboPendienteId.Value && r.estado_id == 2, ct);
                if (!pendienteVivo)
                    return ResponseModelDto.Fail("El recibo pendiente ya fue procesado o no existe.");
            }

            await _context.SaveChangesAsync(ct);

            // ---------- Folio + adm_pago + aplicaciones (el modelo nuevo) ----------
            var numeroRecibo = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT public.fn_adm_siguiente_correlativo_documento(@CompanyId, @Tipo, @Canal)",
                new { CompanyId = companyId, Tipo = TipoDocumentoSecuencia.ReciboPago, Canal = dto.Canal },
                dbTransaction, cancellationToken: ct));
            if (string.IsNullOrWhiteSpace(numeroRecibo))
                return ResponseModelDto.Fail(
                    "No hay serie de folios RECIBO_PAGO activa para la empresa (adm_documento_secuencia).");

            var pago = new adm_pago
            {
                company_id = companyId,
                numero_recibo = numeroRecibo,
                cliente_clave = dto.ClienteClave,
                fecha = fechaHoy,
                canal_id = dto.Canal,
                tipo_transaccion_id = dto.Canal == CanalCobro.Banco ? TipoTransaccion.PagoBanco : TipoTransaccion.PagoCaja,
                estado_id = EstadoPago.Aplicado,
                monto_total = montoTotal,
                forma_pago = dto.FormaPago,
                banco_cuenta_id = dto.BancoCuentaId.HasValue ? (int?)dto.BancoCuentaId.Value : null,
                ban_kardex_id = movimientoBanco?.BanKardexId,
                sesion_caja_id = sesionCajaId,
                referencia_externa = referencia,
                transaccion_abonado_ide = null,   // F7 H2c: sin espejo legacy
                usuario = usuario
            };
            foreach (var (facturaId, detalleId, monto) in lineasAplicadas)
            {
                pago.aplicaciones.Add(new adm_pago_aplicacion
                {
                    company_id = companyId,
                    documento_tipo = DocumentoCobroTipo.Factura,
                    factura_id = facturaId,
                    factura_detalle_id = detalleId,
                    monto_aplicado = monto
                });
            }
            foreach (var (planCuotaId, monto) in cuotasAplicadas)
            {
                pago.aplicaciones.Add(new adm_pago_aplicacion
                {
                    company_id = companyId,
                    documento_tipo = DocumentoCobroTipo.CuotaPlan,
                    plan_cuota_id = planCuotaId,
                    monto_aplicado = monto
                });
            }
            foreach (var (notaDebitoId, monto) in ndsAplicadas)
            {
                pago.aplicaciones.Add(new adm_pago_aplicacion
                {
                    company_id = companyId,
                    documento_tipo = DocumentoCobroTipo.NotaDebito,
                    nota_debito_id = notaDebitoId,
                    monto_aplicado = monto
                });
            }
            _context.adm_pagos.Add(pago);
            await _context.SaveChangesAsync(ct);

            // F7 H1: si el cobro vino de un recibo-para-banco pendiente, el
            // registro formal queda APLICADO con el documento que lo cobró.
            // (El trigger de conciliación pudo marcarlo CUBIERTO un instante
            // antes si la factura quedó saldada — este es el estado correcto.)
            if (dto.ReciboPendienteId.HasValue)
            {
                var reciboPendiente = await _context.adm_recibo_banco_pendientes
                    .FirstOrDefaultAsync(r => r.recibo_pendiente_id == dto.ReciboPendienteId.Value
                                              && r.cobrado_pago_id == null, ct);
                if (reciboPendiente is not null)
                {
                    reciboPendiente.estado_id = EstadoPago.Aplicado;
                    reciboPendiente.cobrado_pago_id = pago.pago_id;
                    reciboPendiente.anulado_por = null;
                    reciboPendiente.anulado_en = null;
                    reciboPendiente.motivo_anulacion = null;
                    await _context.SaveChangesAsync(ct);
                }
            }

            // ---------- Cortes: cancelar órdenes si el cliente queda en cero ----------
            if (saldoActualCliente - montoTotal <= 0m)
            {
                await _corteMasivoService.CancelarOrdenesCorteClienteAsync(dto.ClienteClave, usuario, ct);
            }

            // ---------- Comprobante contable (efectivo; banco ya posteó su partida) ----------
            long? polizaId = null;
            var polizaEncolada = false;
            if (!integrarBancos)
            {
                var config = await IntegracionContableConfigSql.ObtenerConfigAsync(connection, companyId, dbTransaction, ct);
                if (config is not null && config.ActivoCaja)
                {
                    var cuentaCaja = await IntegracionContableConfigSql.ResolverCuentaAsync(
                        connection, companyId, "CAJA", dbTransaction, ct);

                    var aplicacionesCxc = await IntegracionContableConfigSql.ResolverAplicacionesCxcAsync(
                        connection,
                        companyId,
                        config.ModoCxc,
                        aplicacionesContables,
                        facturaPrincipal?.categoria_servicio_id,
                        facturaPrincipal?.con_medicion,
                        dbTransaction,
                        ct);

                    var lineas = IntegracionContableConfigSql.ArmarLineasCobro(
                        cuentaCaja,
                        aplicacionesCxc,
                        descripcionContable);

                    // F7 H2c: el comprobante se identifica por el DOCUMENTO del
                    // motor (pago_id), no por el ide del espejo legacy — el
                    // espejo muere en este mismo hito y su ide dejaría al
                    // asiento sin ancla para reversar.
                    polizaId = await IntegracionContableConfigSql.GenerarComprobanteAsync(
                        connection,
                        companyId,
                        documentoContable.Modulo,
                        documentoContable.Documento,
                        pago.pago_id,
                        $"{documentoContable.Documento}-{pago.pago_id}",
                        fechaHoy,
                        descripcionContable,
                        usuario,
                        lineas,
                        dbTransaction,
                        ct);

                    polizaEncolada = polizaId is null;
                    if (polizaId.HasValue)
                    {
                        pago.poliza_id = polizaId;
                        await _context.SaveChangesAsync(ct);
                    }
                }
            }

            if (tx is not null) await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(new CobroResultadoDto
            {
                PagoId = pago.pago_id,
                NumeroRecibo = numeroRecibo,
                MontoTotal = montoTotal,
                NuevoSaldoCliente = saldoActualCliente - montoTotal,
                PolizaId = polizaId,
                PolizaEncolada = polizaEncolada,
                BanKardexId = movimientoBanco?.BanKardexId,
                Aplicaciones = resultados
            }, "Cobro registrado correctamente.");
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            if (movimientoBanco.HasValue && dto.BancoCuentaId.HasValue)
            {
                await TryCompensarMovimientoBancarioAsync(dto.BancoCuentaId.Value, movimientoBanco.Value.BanKardexId, usuario, ct);
            }
            return ResponseModelDto.Fail($"Error al registrar el cobro: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> ReversarCobroAsync(CobroReversoDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _currentCompanyService.GetCompanyId();
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();

        var pago = await _context.adm_pagos
            .Include(p => p.aplicaciones)
            .FirstOrDefaultAsync(p => p.company_id == companyId && p.pago_id == dto.PagoId, ct);
        if (pago is null)
            return ResponseModelDto.Fail("No se encontró el cobro indicado.");
        if (pago.estado_id != EstadoPago.Aplicado)
            return ResponseModelDto.Fail("El cobro ya se encuentra anulado o reversado.");
        if (pago.canal_id == CanalCobro.Banco)
            return ResponseModelDto.Fail(
                "Este pago proviene del canal bancario y debe reversarse por ese canal (sp_ban_ws_reversar).");

        var facturaIds = pago.aplicaciones
            .Where(a => a.factura_id.HasValue)
            .Select(a => a.factura_id!.Value)
            .Distinct()
            .ToList();

        // F7 H2c: los cobros nuevos no tienen espejo; los pre-corte (data de
        // prueba local) todavía lo traen y se marcan anulados por cortesía.
        transaccion_abonado? espejo = null;
        if (pago.transaccion_abonado_ide.HasValue)
        {
            espejo = await _context.transaccion_abonados
                .FirstOrDefaultAsync(t => t.company_id == companyId && t.ide == pago.transaccion_abonado_ide.Value, ct);
        }

        var ownsTx = _context.Database.CurrentTransaction is null;
        await using var tx = ownsTx ? await _context.Database.BeginTransactionAsync(ct) : null;
        try
        {
            // Restituir saldos EXACTAMENTE en las líneas aplicadas (mejor que la
            // heurística legacy: la tabla de aplicación sabe qué se cobró dónde).
            foreach (var facturaId in facturaIds)
            {
                var factura = await _context.facturas
                    .FromSqlInterpolated($"SELECT * FROM public.factura WHERE company_id = {companyId} AND id = {facturaId} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
                if (factura is null)
                    return ResponseModelDto.Fail($"No se encontró la factura {facturaId} del cobro.");

                var detalles = await _context.factura_detalles
                    .Where(d => d.factura_id == facturaId)
                    .ToListAsync(ct);

                foreach (var apl in pago.aplicaciones.Where(a => a.factura_id == facturaId && a.factura_detalle_id.HasValue))
                {
                    var detalle = detalles.FirstOrDefault(d => d.id == apl.factura_detalle_id!.Value);
                    if (detalle is not null)
                    {
                        var tope = detalle.montovalor ?? 0m;
                        detalle.montovalor_saldo = Math.Min(tope, (detalle.montovalor_saldo ?? 0m) + apl.monto_aplicado);
                    }
                }
                // Aplicaciones sin línea (remanente al encabezado legacy): sin
                // restitución de detalle — mismo comportamiento del flujo viejo.

                var saldoRestante = detalles.Sum(d => d.montovalor_saldo ?? 0m);
                var totalFactura = detalles.Sum(d => d.montovalor ?? 0m);
                if (saldoRestante >= totalFactura && totalFactura > 0)
                {
                    factura.estado = "A";
                    factura.fechapago = null;
                    factura.recolectora = null;
                }
                else
                {
                    factura.estado = "B";
                }
            }

            // F6: restituir cuotas de plan aplicadas (documento_tipo = 2) y
            // reabrir el plan si el cobro lo había completado.
            var planesReabrir = new HashSet<int>();
            foreach (var apl in pago.aplicaciones.Where(a => a.plan_cuota_id.HasValue))
            {
                var cuota = await _context.cln_plan_pago_dtls
                    .FromSqlInterpolated($"SELECT * FROM public.cln_plan_pago_dtl WHERE company_id = {companyId} AND id = {apl.plan_cuota_id!.Value} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
                if (cuota is null)
                    return ResponseModelDto.Fail($"No se encontró la cuota {apl.plan_cuota_id} del cobro.");

                var tope = cuota.valorcuota ?? 0m;
                cuota.saldo_cuota = Math.Min(tope, cuota.saldo_cuota + apl.monto_aplicado);
                cuota.estado_id = cuota.saldo_cuota >= tope
                    ? EstadoDocumentoComercial.Activa
                    : EstadoDocumentoComercial.ParcialmenteAbonada;
                cuota.estadopago = "Pendiente"; // legacy hasta F7
                cuota.usuariomodificacion = usuario;
                cuota.fechamodificacion = DateTime.Now;
                if (cuota.idhdr.HasValue) planesReabrir.Add(cuota.idhdr.Value);
            }

            foreach (var planId in planesReabrir)
            {
                var plan = await _context.cln_plan_pago_hdrs.FirstOrDefaultAsync(h => h.id == planId, ct);
                if (plan is not null && plan.estado_id == EstadoPlan.Completado)
                {
                    plan.estado_id = EstadoPlan.Activo;
                    plan.estadopago = "Pendiente"; // legacy hasta F7
                    plan.usuariomodificacion = usuario;
                    plan.fechamodificacion = DateTime.Now;
                }
            }

            // F7 H2b: restituir notas de débito aplicadas (documento_tipo = 3).
            foreach (var apl in pago.aplicaciones.Where(a => a.nota_debito_id.HasValue))
            {
                var nd = await _context.adm_nota_debitos
                    .FromSqlInterpolated($"SELECT * FROM public.adm_nota_debito WHERE company_id = {companyId} AND nota_debito_id = {apl.nota_debito_id!.Value} FOR UPDATE")
                    .FirstOrDefaultAsync(ct);
                if (nd is null)
                    return ResponseModelDto.Fail($"No se encontró la nota de débito {apl.nota_debito_id} del cobro.");

                nd.saldo_pendiente = Math.Min(nd.total_nota, nd.saldo_pendiente + apl.monto_aplicado);
            }

            // Espejo legacy: marcar anulado (NUNCA borrar — regla única del motor).
            if (espejo is not null)
            {
                espejo.estado = "A";
                espejo.usuario = usuario;
                espejo.descripcion = $"REVERSADO: {dto.Motivo}";
            }

            // Contabilidad / banco
            if (pago.ban_kardex_id.HasValue && pago.banco_cuenta_id.HasValue)
            {
                await _banTransaccionesService.AnularMovimientoAsync(
                    pago.banco_cuenta_id.Value,
                    pago.ban_kardex_id.Value,
                    $"Reverso cobro {pago.numero_recibo}",
                    usuario,
                    ct);
            }
            else
            {
                var connection = _context.Database.GetDbConnection();
                var dbTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();

                // F7 H2c: el comprobante se reversa por el DOCUMENTO del motor
                // (pago_id), la misma identidad con que se posteó. El tipo
                // legacy solo resuelve el par módulo/documento contable.
                var documentoContable = ResolverDocumentoContable(espejo?.tipotransaccion ?? "202");
                await IntegracionContableConfigSql.RevertirComprobanteAsync(
                    connection,
                    companyId,
                    documentoContable.Modulo,
                    new[] { documentoContable.Documento },
                    pago.pago_id,
                    usuario,
                    dbTransaction,
                    ct);
            }

            pago.estado_id = EstadoPago.Anulado;
            pago.motivo_reverso = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim();
            pago.actualizado_en = DateTime.UtcNow;

            await _context.SaveChangesAsync(ct);
            if (tx is not null) await tx.CommitAsync(ct);

            return ResponseModelDto.Ok(null, "Cobro reversado correctamente.");
        }
        catch (Exception ex)
        {
            if (tx is not null) await tx.RollbackAsync(ct);
            return ResponseModelDto.Fail($"Error al reversar el cobro: {ex.Message}");
        }
    }

    public async Task<IReadOnlyList<CobroDelDiaDto>> ListarCobrosDelDiaAsync(
        DateTime? fecha, string? usuario, int? cajaFisicaId = null, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        // Mismo criterio que el registro: día LOCAL del servidor.
        var dia = DateOnly.FromDateTime(fecha?.Date ?? DateTime.Now.Date);

        // "Cobros del día" = lo REGISTRADO ese día (creado_en), no la fecha valor:
        // desde H4b la caja puede registrar cobros con fecha retroactiva (la
        // recaudación del lector llega después) y esos deben verse en el arqueo
        // del día en que se teclearon, aunque su `fecha` sea anterior.
        var desde = dia.ToDateTime(TimeOnly.MinValue);
        var hastaExcl = desde.AddDays(1);

        var query = from p in _context.adm_pagos.AsNoTracking()
                    where p.company_id == companyId
                          && p.creado_en >= desde && p.creado_en < hastaExcl
                    join c in _context.cliente_maestros.AsNoTracking()
                        on p.cliente_clave equals c.maestro_cliente_clave into clientes
                    from c in clientes.DefaultIfEmpty()
                    join s in _context.sesion_cajas.AsNoTracking()
                        on p.sesion_caja_id equals (int?)s.id into sesiones
                    from s in sesiones.DefaultIfEmpty()
                    join k in _context.adm_cajas.AsNoTracking()
                        on s.caja_fisica_id equals (int?)k.caja_id into cajas
                    from k in cajas.DefaultIfEmpty()
                    orderby p.pago_id descending
                    select new
                    {
                        p.pago_id,
                        p.numero_recibo,
                        p.fecha,
                        p.cliente_clave,
                        ClienteNombre = c != null ? c.maestro_cliente_nombre : string.Empty,
                        p.monto_total,
                        p.forma_pago,
                        p.estado_id,
                        p.usuario,
                        CajaNombre = k != null ? k.nombre : null,
                        CajaFisicaId = s != null ? s.caja_fisica_id : null
                    };

        if (!string.IsNullOrWhiteSpace(usuario))
        {
            var u = usuario.Trim();
            query = query.Where(x => x.usuario == u);
        }

        if (cajaFisicaId is > 0)
        {
            query = query.Where(x => x.CajaFisicaId == cajaFisicaId.Value);
        }

        var items = await query.Take(500).ToListAsync(ct);
        return items.Select(x => new CobroDelDiaDto
        {
            PagoId = x.pago_id,
            NumeroRecibo = x.numero_recibo,
            Fecha = x.fecha.ToDateTime(TimeOnly.MinValue),
            ClienteClave = x.cliente_clave,
            ClienteNombre = x.ClienteNombre ?? string.Empty,
            MontoTotal = x.monto_total,
            FormaPago = x.forma_pago,
            EstadoId = x.estado_id,
            Estado = x.estado_id switch
            {
                EstadoPago.Aplicado => "APLICADO",
                EstadoPago.Pendiente => "PENDIENTE",
                EstadoPago.Anulado => "ANULADO",
                EstadoPago.Reversado => "REVERSADO",
                _ => x.estado_id.ToString()
            },
            Usuario = x.usuario,
            CajaNombre = x.CajaNombre
        }).ToList();
    }

    // ------------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------------

    /// <summary>
    /// F6: parte contable del pago de una cuota de plan — se distribuye entre
    /// servicios por el desglose porcentual configurado (la cuota no tiene
    /// líneas por servicio propias; es la misma aproximación del posteo
    /// legacy). Sin configuración, todo a la CxC general (servicio null).
    /// El redondeo por ítem se cierra contra el último (restos al final).
    /// </summary>
    private static List<(string? ServicioCodigo, decimal Monto)> DistribuirCuotaContable(
        decimal monto, Dictionary<string, decimal> porcentajes)
    {
        if (porcentajes.Count == 0 || porcentajes.Values.Sum() != 100m)
        {
            return [(null, monto)];
        }

        var resultado = new List<(string?, decimal)>();
        var acumulado = 0m;
        foreach (var (codigo, pct) in porcentajes.OrderBy(p => p.Key))
        {
            var parte = Math.Round(monto * pct / 100m, 2, MidpointRounding.AwayFromZero);
            resultado.Add((codigo, parte));
            acumulado += parte;
        }

        var resto = monto - acumulado;
        if (resto != 0m && resultado.Count > 0)
        {
            var ultimo = resultado[^1];
            resultado[^1] = (ultimo.Item1, ultimo.Item2 + resto);
        }

        return resultado.Where(r => r.Item2 > 0m).ToList();
    }

    /// <summary>
    /// F7 H2b: parte contable del cobro de una nota de débito — se distribuye
    /// por las LÍNEAS propias de la ND (proporción de sus servicios), de modo
    /// que el Haber acredite la misma CxC analítica que la emisión debitó.
    /// Sin líneas con servicio, todo a la CxC general (servicio null).
    /// </summary>
    private async Task<List<(string? ServicioCodigo, decimal Monto)>> DistribuirNdContableAsync(
        long notaDebitoId, decimal monto, CancellationToken ct)
    {
        var lineas = await _context.adm_nota_debito_detalles
            .AsNoTracking()
            .Where(d => d.nota_debito_id == notaDebitoId
                        && d.servicio_codigo != null
                        && d.monto_total > 0)
            .Select(d => new { d.servicio_codigo, d.monto_total })
            .ToListAsync(ct);

        var total = lineas.Sum(l => l.monto_total);
        if (lineas.Count == 0 || total <= 0)
        {
            return [(null, monto)];
        }

        var resultado = new List<(string?, decimal)>();
        var acumulado = 0m;
        for (var i = 0; i < lineas.Count; i++)
        {
            var parte = i == lineas.Count - 1
                ? monto - acumulado
                : Math.Round(monto * lineas[i].monto_total / total, 2, MidpointRounding.AwayFromZero);
            resultado.Add((lineas[i].servicio_codigo, parte));
            acumulado += parte;
        }

        return resultado.Where(r => r.Item2 > 0m).ToList();
    }

    /// <summary>
    /// Porcentajes de aplicación por servicio (adm_desglose_abono_porcentaje,
    /// mantenimiento /tarifario/desglose-abonos). Solo gobiernan la aplicación
    /// si suman exactamente 100; sin configuración el derrame es FIFO.
    /// </summary>
    private static async Task<Dictionary<string, decimal>> CargarPorcentajesDesgloseAsync(
        System.Data.Common.DbConnection connection, long companyId, IDbTransaction? transaction, CancellationToken ct)
    {
        var filas = await connection.QueryAsync<(string Codigo, decimal Porcentaje)>(new CommandDefinition(
            "SELECT item_codigo, porcentaje FROM public.adm_desglose_abono_porcentaje WHERE company_id = @CompanyId",
            new { CompanyId = companyId }, transaction, cancellationToken: ct));
        return filas.ToDictionary(f => f.Codigo.Trim().ToUpperInvariant(), f => f.Porcentaje);
    }

    /// <summary>
    /// Aplica un monto a las líneas de una factura y registra la aplicación.
    /// Con porcentajes configurados (suma 100): PRIORIDAD a las líneas NO
    /// configuradas (otros cargos) en FIFO, y el resto se distribuye entre los
    /// servicios configurados presentes según su porcentaje (renormalizado),
    /// con redistribución al saturarse una línea y residuo de redondeo en FIFO.
    /// Sin configuración: FIFO puro (comportamiento previo). Devuelve el
    /// remanente no aplicable a líneas (facturas legacy con saldo solo en
    /// encabezado).
    /// </summary>
    private static decimal AplicarMontoALineas(
        int facturaId,
        IReadOnlyList<factura_detalle> detalles,
        decimal monto,
        IReadOnlyDictionary<string, decimal> porcentajes,
        List<(int FacturaId, int? DetalleId, decimal Monto)> lineasAplicadas,
        List<(string? ServicioCodigo, decimal Monto)> aplicacionesContables)
    {
        var restante = monto;

        static decimal Saldo(factura_detalle d) => d.montovalor_saldo ?? d.montovalor ?? 0m;
        static string? ServicioCodigo(factura_detalle d) =>
            string.IsNullOrWhiteSpace(d.tiposervicio) ? d.codigo : d.tiposervicio;
        static string Clave(factura_detalle d) => ServicioCodigo(d)?.Trim().ToUpperInvariant() ?? string.Empty;

        void Aplicar(factura_detalle d, decimal aplicado)
        {
            d.montovalor_saldo = Saldo(d) - aplicado;
            restante -= aplicado;
            lineasAplicadas.Add((facturaId, d.id, aplicado));
            aplicacionesContables.Add((ServicioCodigo(d), aplicado));
        }

        var usarPorcentajes = porcentajes.Count > 0 && porcentajes.Values.Sum() == 100m;

        if (usarPorcentajes)
        {
            // 1) Prioridad: líneas fuera de la configuración (otros cargos) en FIFO.
            foreach (var d in detalles)
            {
                if (restante <= 0) break;
                var s = Saldo(d);
                if (s <= 0 || porcentajes.ContainsKey(Clave(d))) continue;
                Aplicar(d, Math.Min(restante, s));
            }

            // 2) Distribución porcentual (renormalizada a los servicios presentes),
            //    con vueltas de redistribución cuando una línea se satura.
            for (var vuelta = 0; vuelta < 10 && restante > 0m; vuelta++)
            {
                var activos = detalles
                    .Where(d => Saldo(d) > 0 && porcentajes.ContainsKey(Clave(d)))
                    .GroupBy(Clave)
                    .ToList();
                if (activos.Count == 0) break;

                var pesoTotal = activos.Sum(g => porcentajes[g.Key]);
                if (pesoTotal <= 0) break;

                var presupuesto = restante;
                var asignado = false;
                foreach (var grupo in activos)
                {
                    var objetivo = decimal.Round(
                        presupuesto * porcentajes[grupo.Key] / pesoTotal, 2, MidpointRounding.AwayFromZero);
                    foreach (var d in grupo)
                    {
                        if (objetivo <= 0 || restante <= 0) break;
                        var s = Saldo(d);
                        if (s <= 0) continue;
                        var aplicado = Math.Min(Math.Min(objetivo, s), restante);
                        if (aplicado <= 0) continue;
                        Aplicar(d, aplicado);
                        objetivo -= aplicado;
                        asignado = true;
                    }
                }
                if (!asignado) break;
            }
        }

        // FIFO: derrame normal sin configuración, o residuo de redondeo con ella.
        foreach (var d in detalles)
        {
            if (restante <= 0) break;
            var s = Saldo(d);
            if (s <= 0) continue;
            Aplicar(d, Math.Min(restante, s));
        }

        return restante;
    }

    private async Task<ResponseModelDto?> BuscarCobroPorReferenciaAsync(long companyId, string referencia, CancellationToken ct)
    {
        var existente = await _context.adm_pagos
            .AsNoTracking()
            .Where(p => p.company_id == companyId && p.referencia_externa == referencia)
            .Select(p => new { p.pago_id, p.numero_recibo, p.monto_total, p.estado_id })
            .FirstOrDefaultAsync(ct);
        if (existente is null) return null;

        if (existente.estado_id != EstadoPago.Aplicado)
            return ResponseModelDto.Fail(
                $"La referencia {referencia} ya fue usada y su cobro está anulado/reversado; use una referencia nueva.");

        return ResponseModelDto.Ok(new CobroResultadoDto
        {
            PagoId = existente.pago_id,
            NumeroRecibo = existente.numero_recibo,
            MontoTotal = existente.monto_total,
            Idempotente = true
        }, "Cobro ya aplicado con esa referencia (idempotente).");
    }

    /// <summary>
    /// Documento contable ÚNICO para todo cobro del motor: VENTAS/REC
    /// (decisión confirmada con el contador 2026-07-26 — plan §9.4; la
    /// distinción REC/ABO era un accidente de que existían dos pantallas).
    /// Los ABO históricos pre-motor no se tocan; su reverso va por el camino
    /// legacy (F2b) o por la búsqueda [REC, ABO] del reverso del motor.
    /// </summary>
    private static (string Modulo, string Documento) ResolverDocumentoContable(string _) =>
        ("VENTAS", "REC");

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@CompanyId, @ClienteClave)";
        var saldo = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
            sql,
            new { CompanyId = companyId, ClienteClave = clienteClave },
            _context.Database.CurrentTransaction?.GetDbTransaction(),
            cancellationToken: ct));
        return saldo ?? 0m;
    }

    private async Task<string> ObtenerPeriodoActualCodigoAsync(CancellationToken ct)
    {
        var periodo = await _context.adm_periodo_comercials
            .AsNoTracking()
            .Where(p => p.status_id == EstadoPeriodoComercial.Abierto)
            .OrderByDescending(p => p.anio)
            .ThenByDescending(p => p.mes)
            .Select(p => new { p.anio, p.mes })
            .FirstOrDefaultAsync(ct);

        return periodo is null
            ? DateTime.UtcNow.ToString("yyyyMM")
            : $"{periodo.anio:D4}{periodo.mes:D2}";
    }

    private async Task<string?> ResolverBancoCodigoAsync(long? bancoCuentaId, string? bancoFallback, CancellationToken ct)
    {
        var bancoNormalizado = string.IsNullOrWhiteSpace(bancoFallback) ? null : bancoFallback.Trim();
        if (!bancoCuentaId.HasValue || bancoCuentaId.Value <= 0)
        {
            return bancoNormalizado;
        }

        var cuenta = await _context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.banco_cuenta_id == bancoCuentaId.Value && c.activo)
            .Select(c => new
            {
                c.code,
                BancoCode = c.ban_banco != null ? c.ban_banco.code : null
            })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null) return bancoNormalizado;
        return cuenta.BancoCode?.Trim() ?? cuenta.code?.Trim() ?? bancoNormalizado;
    }

    private async Task TryCompensarMovimientoBancarioAsync(long bancoCuentaId, long banKardexId, string usuario, CancellationToken ct)
    {
        try
        {
            await _banTransaccionesService.AnularMovimientoAsync(
                bancoCuentaId, banKardexId, "Compensación de cobro fallido", usuario, ct);
        }
        catch
        {
            // Fail silent: la compensación es best-effort tras un rollback.
        }
    }
}
