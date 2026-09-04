using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Almacen;
using SIAD.Services.Infrastructure;
using SIAD.Services.Presupuesto;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Implementación de las cuentas por pagar unificadas.
/// <para>
/// <b>Lectura:</b> Dapper contra <c>fn_prv_cxp_documentos</c> / <c>fn_prv_cxp_resumen</c>
/// (script <c>Database/2026-08-22_prv_cxp_unificada.sql</c>), que a su vez se apoyan en
/// <c>fn_prv_estado_cuenta_documentos</c>. Las reglas de vigencia viven en la BD, en un solo
/// lugar: aquí no se decide qué es deuda viva.
/// </para>
/// <para>
/// <b>Pago:</b> este servicio NO reimplementa la mecánica de pago. Cada documento se paga con
/// el mismo servicio que ya lo pagaba —<see cref="ICompraCxpService"/> para las facturas,
/// <see cref="IOrdenesPagoDirectoService"/> para los compromisos—, así que el movimiento
/// bancario, el cheque, la partida contable y el registro fiscal de la retención salen
/// idénticos a los del pago individual. Lo único que agrega el lote es la transacción que los
/// envuelve: ambos servicios reutilizan la transacción ambiente del <see cref="SiadDbContext"/>,
/// de modo que si un documento falla no queda ninguno registrado.
/// </para>
/// <para>
/// <b>Tenancy:</b> Dapper no pasa por el filtro global del contexto, así que la empresa se
/// resuelve con <see cref="ICurrentCompanyService"/> y viaja explícita en cada función.
/// </para>
/// </summary>
public sealed class CuentasPorPagarService : ICuentasPorPagarService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly ICompraCxpService _compras;
    private readonly IOrdenesPagoDirectoService _compromisos;

    public CuentasPorPagarService(
        SiadDbContext context,
        ICurrentCompanyService company,
        ICompraCxpService compras,
        IOrdenesPagoDirectoService compromisos)
    {
        _context = context;
        _company = company;
        _compras = compras;
        _compromisos = compromisos;

        // Dapper no sabe pasar DateOnly sin este handler; es idempotente.
        DapperTypeHandlers.EnsureRegistered();
    }

    // ── Lectura ──────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<CxpDocumentoDto>> ListarAsync(
        CxpUnificadaFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new CxpUnificadaFilterDto();
        var connection = await AbrirConexionAsync(ct);

        const string sql = @"
            SELECT origen            AS Origen,
                   documento_id      AS DocumentoId,
                   numero_documento  AS NumeroDocumento,
                   cod_proveedor     AS CodProveedor,
                   proveedor         AS Proveedor,
                   fecha             AS Fecha,
                   fecha_vencimiento AS FechaVencimiento,
                   concepto          AS Concepto,
                   monto             AS Monto,
                   abonado           AS Abonado,
                   saldo             AS Saldo,
                   dias_vencido      AS DiasVencido,
                   estado_id         AS EstadoId,
                   procesado         AS Procesado
            FROM public.fn_prv_cxp_documentos(
                     @CompanyId, @Search, @Origen, @EstadoId, @CodProveedor,
                     @SoloVencidos, @IncluirPagados)";

        var filas = await connection.QueryAsync<CxpDocumentoDto>(
            new CommandDefinition(sql, Parametros(filtro), cancellationToken: ct));

        return new List<CxpDocumentoDto>(filas);
    }

    public async Task<CxpResumenDto> ObtenerResumenAsync(
        CxpUnificadaFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new CxpUnificadaFilterDto();
        var connection = await AbrirConexionAsync(ct);

        const string sql = @"
            SELECT saldo_total            AS SaldoTotal,
                   saldo_vencido          AS SaldoVencido,
                   saldo_vence_7dias      AS SaldoVence7Dias,
                   saldo_compras          AS SaldoCompras,
                   saldo_compromisos      AS SaldoCompromisos,
                   documentos_pendientes  AS DocumentosPendientes,
                   compras_pendientes     AS ComprasPendientes,
                   compromisos_pendientes AS CompromisosPendientes,
                   documentos_vencidos    AS DocumentosVencidos
            FROM public.fn_prv_cxp_resumen(
                     @CompanyId, @Search, @Origen, @EstadoId, @CodProveedor,
                     @SoloVencidos, @IncluirPagados)";

        var resumen = await connection.QuerySingleOrDefaultAsync<CxpResumenDto>(
            new CommandDefinition(sql, Parametros(filtro), cancellationToken: ct));

        return resumen ?? new CxpResumenDto();
    }

    private object Parametros(CxpUnificadaFilterDto filtro) => new
    {
        CompanyId = EnsureCompanyId(),
        Search = string.IsNullOrWhiteSpace(filtro.Search) ? null : filtro.Search.Trim(),
        // La función espera 0 = ambas ramas; null aquí sería un filtro sin efecto.
        Origen = filtro.Origen is OrigenDocumentoProveedor.Compra or OrigenDocumentoProveedor.Compromiso
            ? filtro.Origen.Value
            : (short)0,
        EstadoId = filtro.EstadoId,
        CodProveedor = string.IsNullOrWhiteSpace(filtro.CodProveedor) ? null : filtro.CodProveedor.Trim(),
        SoloVencidos = filtro.SoloVencidos,
        IncluirPagados = filtro.IncluirPagados
    };

    // ── Pago en lote ─────────────────────────────────────────────────────────────

    public async Task<CxpLoteResultadoDto> PagarLoteAsync(
        CxpLoteUpsertDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        EnsureCompanyId();

        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        var metodo = (dto.MetodoPago ?? string.Empty).Trim().ToUpperInvariant();
        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var lineas = dto.Lineas ?? new List<CxpLoteLineaDto>();

        if (lineas.Count == 0)
        {
            throw new InvalidOperationException("Seleccione al menos un documento para pagar.");
        }

        var claves = new HashSet<string>(StringComparer.Ordinal);
        var hayCompromisos = false;
        foreach (var l in lineas)
        {
            if (!claves.Add($"{l.Origen}-{l.DocumentoId}"))
            {
                throw new InvalidOperationException("Un mismo documento no puede ir dos veces en el lote.");
            }
            if (l.Origen == OrigenDocumentoProveedor.Compromiso)
            {
                hayCompromisos = true;
            }
        }

        var esBancario = MetodoPagoCompra.EsBancario(metodo);
        if (esBancario && dto.BancoCuentaId is not > 0)
        {
            throw new InvalidOperationException("Seleccione el banco y la cuenta de donde sale el pago.");
        }

        // Los documentos vivos, para validar contra el saldo real y nombrar cada pago en el
        // resultado. Se piden pendientes: un documento ya saldado no debe entrar al lote.
        var vivos = new Dictionary<string, CxpDocumentoDto>(StringComparer.Ordinal);
        foreach (var d in await ListarAsync(new CxpUnificadaFilterDto(), ct))
        {
            vivos[d.ClaveGrid] = d;
        }

        // La cuenta contable de la cuenta bancaria: la necesita el compromiso para armar su
        // partida. Solo se resuelve si hay compromisos bancarios en el lote.
        long? cuentaOrigenCompromiso = null;
        if (esBancario && hayCompromisos)
        {
            foreach (var cuenta in await _compromisos.GetCuentasContraProcesamientoAsync(ct))
            {
                if (cuenta.BancoCuentaId == dto.BancoCuentaId)
                {
                    cuentaOrigenCompromiso = cuenta.AccountId;
                    break;
                }
            }

            if (cuentaOrigenCompromiso is not > 0)
            {
                throw new InvalidOperationException(
                    "La cuenta bancaria elegida no tiene cuenta contable asociada; no se puede pagar un compromiso con ella.");
            }
        }

        foreach (var linea in lineas)
        {
            if (linea.Monto <= 0m)
            {
                throw new InvalidOperationException("El monto de cada documento debe ser mayor que cero.");
            }
            if (!vivos.TryGetValue($"{linea.Origen}-{linea.DocumentoId}", out var doc))
            {
                throw new InvalidOperationException(
                    "Uno de los documentos seleccionados ya no está pendiente (lo pagaron o lo anularon). Recargue la pantalla.");
            }
            if (linea.Monto - doc.Saldo > 0.01m)
            {
                throw new InvalidOperationException(
                    $"El monto de {doc.NumeroDocumento} ({linea.Monto:N2}) supera su saldo pendiente ({doc.Saldo:N2}).");
            }
        }

        var resultado = new CxpLoteResultadoDto { Success = true };

        // Todo o nada: los dos servicios de pago reutilizan la transacción ambiente. Si ya hay
        // una abierta (tests bajo BEGIN … ROLLBACK, o una operación mayor que nos envuelve) se
        // usa esa y el commit lo hace quien la abrió; abrir otra encima reventaría con
        // "A transaction is already in progress".
        var ambiente = _context.Database.CurrentTransaction;
        var tx = ambiente ?? await _context.Database.BeginTransactionAsync(ct);
        var propia = ambiente is null;
        try
        {
            foreach (var linea in lineas)
            {
                var doc = vivos[$"{linea.Origen}-{linea.DocumentoId}"];
                var pago = doc.EsCompra
                    ? await PagarCompraAsync(linea, doc, dto, metodo, fecha, usuario, ct)
                    : await PagarCompromisoAsync(linea, doc, dto, metodo, fecha, cuentaOrigenCompromiso, usuario, ct);

                resultado.Pagos.Add(pago);
            }

            if (propia) await tx.CommitAsync(ct);
        }
        catch
        {
            if (propia) await tx.RollbackAsync(ct);
            throw;
        }
        finally
        {
            if (propia) await tx.DisposeAsync();
        }

        var proveedoresPagados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pago in resultado.Pagos)
        {
            resultado.TotalAplicado += pago.MontoAplicado;
            resultado.TotalRetenido += pago.Retenido;
            proveedoresPagados.Add(pago.CodProveedor);
        }

        resultado.TotalNeto = resultado.TotalAplicado - resultado.TotalRetenido;
        resultado.Desembolsos = proveedoresPagados.Count;
        resultado.Message = $"Se registraron {resultado.Pagos.Count} pago(s) por {resultado.TotalNeto:N2}.";

        return resultado;
    }

    /// <summary>Una factura de compra: el DTO del pago individual, tal cual.</summary>
    private async Task<CxpLotePagoResultadoDto> PagarCompraAsync(
        CxpLoteLineaDto linea, CxpDocumentoDto doc, CxpLoteUpsertDto dto,
        string metodo, DateOnly fecha, string usuario, CancellationToken ct)
    {
        var abono = new CompraCxpAbonoUpsertDto
        {
            Monto = linea.Monto,
            MetodoPago = metodo,
            BancoCuentaId = dto.BancoCuentaId,
            CuentaContableId = dto.CuentaContableId,
            Fecha = fecha,
            Observaciones = dto.Observaciones,
            Retenciones = linea.Retenciones ?? new List<RetencionAplicadaDto>()
        };

        var r = await _compras.RegistrarAbonoAsync((int)doc.DocumentoId, abono, usuario, ct);

        return new CxpLotePagoResultadoDto
        {
            Origen = doc.Origen,
            DocumentoId = doc.DocumentoId,
            NumeroDocumento = doc.NumeroDocumento,
            CodProveedor = doc.CodProveedor,
            NumeroAbono = r.NumeroAbono,
            MontoAplicado = linea.Monto,
            Retenido = r.Retenido,
            Saldo = r.Saldo,
            EstadoId = r.EstadoId,
            Saldado = r.Saldo <= 0.01m,
            NumeroCheque = r.NumeroCheque
        };
    }

    /// <summary>
    /// Un compromiso. Sin retención va por contra-magnitud (<c>CuentaContraId</c>); con
    /// retención hay que armar la distribución igual que la pantalla de abonos: el origen al
    /// HABER por el neto y una línea por cada retención. El backend agrega el proveedor al DEBE
    /// por el bruto.
    /// </summary>
    private async Task<CxpLotePagoResultadoDto> PagarCompromisoAsync(
        CxpLoteLineaDto linea, CxpDocumentoDto doc, CxpLoteUpsertDto dto,
        string metodo, DateOnly fecha, long? cuentaOrigenBanco, string usuario, CancellationToken ct)
    {
        var retenciones = linea.Retenciones ?? new List<RetencionAplicadaDto>();
        var retenido = 0m;
        foreach (var r in retenciones)
        {
            retenido += r.Monto;
        }

        var neto = linea.Monto - retenido;

        if (retenciones.Count > 0 && neto <= 0m)
        {
            throw new InvalidOperationException(
                $"El neto a pagar de {doc.NumeroDocumento} (monto − retenciones) debe ser mayor que cero.");
        }

        // El compromiso no conoce "EFECTIVO": un pago que no sale de un banco es CONTABLE.
        var metodoCompromiso = metodo switch
        {
            MetodoPagoCompra.Cheque => OrdenPagoDirectoMetodoPago.Cheque,
            MetodoPagoCompra.Transferencia => OrdenPagoDirectoMetodoPago.Transferencia,
            _ => OrdenPagoDirectoMetodoPago.Contable
        };

        var cuentaOrigen = MetodoPagoCompra.EsBancario(metodo) ? cuentaOrigenBanco : dto.CuentaContableId;
        if (cuentaOrigen is not > 0)
        {
            throw new InvalidOperationException(
                $"Falta la cuenta contable de contrapartida para pagar el compromiso {doc.NumeroDocumento}.");
        }

        var abono = new AbonoCompromisoUpsertDto
        {
            Monto = linea.Monto,
            MetodoPago = metodoCompromiso,
            Fecha = fecha.ToDateTime(TimeOnly.MinValue),
            Usuario = usuario
        };

        if (retenciones.Count == 0)
        {
            abono.CuentaContraId = cuentaOrigen;
            abono.BancoCuentaId = dto.BancoCuentaId;
        }
        else
        {
            abono.Lineas.Add(new PartidaLineaOrdenPagoDto
            {
                CuentaId = cuentaOrigen.Value,
                BancoCuentaId = dto.BancoCuentaId,
                Descripcion = doc.Concepto,
                Debito = 0m,
                Credito = neto
            });

            foreach (var r in retenciones)
            {
                abono.Lineas.Add(new PartidaLineaOrdenPagoDto
                {
                    CuentaId = r.CuentaId,
                    Descripcion = doc.Concepto,
                    Debito = 0m,
                    Credito = r.Monto
                });
                abono.Retenciones.Add(r);
            }
        }

        var r2 = await _compromisos.RegistrarAbonoAsync((int)doc.DocumentoId, abono, ct);
        if (!r2.Success)
        {
            // Mensaje de negocio del servicio (saldo cambiado, periodo cerrado…): corta el lote.
            throw new InvalidOperationException(
                $"{doc.NumeroDocumento}: {(string.IsNullOrWhiteSpace(r2.Message) ? "no se pudo registrar el pago." : r2.Message)}");
        }

        return new CxpLotePagoResultadoDto
        {
            Origen = doc.Origen,
            DocumentoId = doc.DocumentoId,
            NumeroDocumento = doc.NumeroDocumento,
            CodProveedor = doc.CodProveedor,
            NumeroAbono = r2.NumeroAbono,
            MontoAplicado = linea.Monto,
            Retenido = retenido,
            Saldo = r2.Saldo,
            EstadoId = r2.Saldo <= 0.01m
                ? EstadoCompraCxp.Pagada
                : EstadoCompraCxp.Parcial,
            Saldado = r2.Pagado || r2.Saldo <= 0.01m,
            NumeroCheque = r2.NumeroCheque
        };
    }

    // ── Helpers ──────────────────────────────────────────────────────────────────

    private long EnsureCompanyId()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        return companyId;
    }

    private async Task<DbConnection> AbrirConexionAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }
}
