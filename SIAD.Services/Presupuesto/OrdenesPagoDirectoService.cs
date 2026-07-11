using System.Data;
using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;
using SIAD.Services.Proveedores;

namespace SIAD.Services.Presupuesto;

public sealed class OrdenesPagoDirectoService : IOrdenesPagoDirectoService
{
    private readonly SiadDbContext _context;
    private readonly IProveedoresService _proveedoresService;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public OrdenesPagoDirectoService(
        SiadDbContext context,
        IProveedoresService proveedoresService,
        ICurrentCompanyService currentCompanyService,
        IHttpContextAccessor httpContextAccessor)
    {
        _context = context;
        _proveedoresService = proveedoresService;
        _currentCompanyService = currentCompanyService;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<IReadOnlyList<OrdenPagoDirectoListItemDto>> GetAsync(
        OrdenPagoDirectoFilterDto? filtro,
        CancellationToken ct = default)
    {
        filtro ??= new OrdenPagoDirectoFilterDto();
        ValidateFilter(filtro);

        var headersQuery = _context.prv_compromiso_hdrs
            .AsNoTracking()
            .AsQueryable();

        if (!filtro.IncludeProcessed)
        {
            headersQuery = headersQuery.Where(x => x.status_transacc != true);
        }

        if (!filtro.IncludeAnuladas)
        {
            headersQuery = headersQuery.Where(x => x.anulado != true);
        }

        if (filtro.NumeroOrden is > 0)
        {
            headersQuery = headersQuery.Where(x => x.numero_orden == filtro.NumeroOrden.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.CodigoProveedor))
        {
            var codigoProveedor = filtro.CodigoProveedor.Trim();
            headersQuery = headersQuery.Where(x => x.cod_proveedor != null && x.cod_proveedor == codigoProveedor);
        }

        var headers = await headersQuery
            .OrderByDescending(x => x.numero_orden)
            .Select(x => new HeaderRow
            {
                NumeroOrden = x.numero_orden,
                CorrelativoProveedor = x.correlativo_proveedor,
                Fecha = x.fecha,
                Rtn = x.rtn,
                Concepto = x.concepto,
                Monto = x.monto,
                CuentaContable = x.cuenta_contable,
                CodigoProveedor = x.cod_proveedor,
                NombreProveedor = x.nombre_proveedor,
                PagarA = x.pagar_a,
                Procesada = x.status_transacc == true,
                Anulada = x.anulado
            })
            .ToListAsync(ct);

        if (headers.Count == 0)
        {
            return Array.Empty<OrdenPagoDirectoListItemDto>();
        }

        var providerMap = await LoadProviderMapAsync(headers, ct);
        var detailCountMap = await LoadDetailCountMapAsync(headers, ct);

        var items = headers
            .Select(x => new OrdenPagoDirectoListItemDto
            {
                NumeroOrden = x.NumeroOrden,
                CorrelativoProveedor = x.CorrelativoProveedor,
                FechaCompromiso = x.Fecha ?? DateTime.MinValue,
                Proveedor = ResolveProveedor(providerMap, x.CodigoProveedor, x.NombreProveedor, x.PagarA),
                Rtn = x.Rtn,
                Concepto = x.Concepto ?? string.Empty,
                Monto = x.Monto,
                CuentaContable = x.CuentaContable,
                CodigoProveedor = x.CodigoProveedor,
                PagarA = x.PagarA,
                Procesada = x.Procesada,
                Anulada = x.Anulada,
                TotalDetalles = detailCountMap.TryGetValue(x.NumeroOrden, out var totalDetalles) ? totalDetalles : 0
            })
            .ToList();

        return ApplyFilter(items, filtro)
            .OrderByDescending(x => x.NumeroOrden)
            .ToList();
    }

    public async Task<IReadOnlyList<OrdenPagoDirectoCentroCostoLookupDto>> GetCentrosCostoAsync(
        CancellationToken ct = default)
    {
        return await (
            from hdr in _context.cnt_centrocostos_hdrs.AsNoTracking()
            join sub in _context.cnt_centro_costos_subgrupos.AsNoTracking()
                on new { hdr.codccg, hdr.codsccg } equals new { sub.codccg, sub.codsccg } into subGroup
            from sub in subGroup.DefaultIfEmpty()
            join grp in _context.cnt_centro_costos_grupos.AsNoTracking()
                on hdr.codccg equals grp.codccg into groupGroup
            from grp in groupGroup.DefaultIfEmpty()
            where hdr.cuenta != null && hdr.cuenta != string.Empty
            orderby hdr.cuenta
            select new OrdenPagoDirectoCentroCostoLookupDto
            {
                CodigoPresupuestario = hdr.cuenta,
                Programa = grp != null ? grp.nombre : hdr.codccg,
                Actividad = sub != null ? sub.nombre : hdr.codsccg,
                ObjetoGasto = hdr.nombre,
                CuentaContable = hdr.contable
            })
            .ToListAsync(ct);
    }

    public async Task<OrdenPagoDirectoDetalleDto?> GetByNumeroOrdenAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            return null;
        }

        var header = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new HeaderRow
            {
                NumeroOrden = x.numero_orden,
                CorrelativoProveedor = x.correlativo_proveedor,
                Fecha = x.fecha,
                Rtn = x.rtn,
                Concepto = x.concepto,
                Monto = x.monto,
                CuentaContable = x.cuenta_contable,
                CodigoProveedor = x.cod_proveedor,
                NombreProveedor = x.nombre_proveedor,
                PagarA = x.pagar_a,
                Procesada = x.status_transacc == true,
                Anulada = x.anulado
            })
            .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            return null;
        }

        var providerMap = await LoadProviderMapAsync(new[] { header }, ct);
        var proveedorMetadata = await LoadProveedorMetadataAsync(header.CodigoProveedor, ct);

        var details = await _context.prv_compromiso_dtls
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .OrderBy(x => x.cod_presupuestario)
            .ThenBy(x => x.descripcion)
            .Select(x => new DetailRow
            {
                CodigoPresupuestario = x.cod_presupuestario,
                Programa = x.programa,
                Actividad = x.actividad,
                ObjetoGasto = x.objeto_gasto,
                CuentaGasto = x.cuenta_gasto,
                Descripcion = x.descripcion,
                Monto = x.monto,
                ConceptoDetalle = x.conceptodtl
            })
            .ToListAsync(ct);

        var centroCostoMap = await LoadCentroCostoDisplayMapAsync(details, ct);
        var partidaContable = await LoadPartidaContableAsync(numeroOrden, ct);

        return new OrdenPagoDirectoDetalleDto
        {
            NumeroOrden = header.NumeroOrden,
            CorrelativoProveedor = header.CorrelativoProveedor,
            Proveedor = ResolveProveedor(providerMap, header.CodigoProveedor, header.NombreProveedor, header.PagarA),
            Rtn = header.Rtn,
            Concepto = header.Concepto ?? string.Empty,
            Monto = header.Monto,
            CuentaContable = header.CuentaContable,
            CuentaContableProveedor = proveedorMetadata?.CuentaContable,
            CodigoProveedor = header.CodigoProveedor,
            FechaCompromiso = header.Fecha,
            PagarA = header.PagarA,
            Procesada = header.Procesada,
            Anulada = header.Anulada,
            Detalles = details
                .Select(x =>
                {
                    centroCostoMap.TryGetValue(x.CodigoPresupuestario ?? string.Empty, out var centroCosto);

                    return new OrdenPagoDirectoDetalleLineaDto
                    {
                        CodigoPresupuestario = x.CodigoPresupuestario,
                        Actividad = centroCosto?.ActividadNombre ?? x.Actividad,
                        Programa = centroCosto?.ProgramaNombre ?? x.Programa,
                        ObjetoGasto = centroCosto?.ObjetoGasto ?? x.ObjetoGasto,
                        CuentaContable = centroCosto?.CuentaContable ?? x.CuentaGasto,
                        Monto = x.Monto,
                        Descripcion = x.Descripcion,
                        ConceptoDetalle = x.ConceptoDetalle
                    };
                })
                .ToList(),
            PartidaContable = partidaContable
        };
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> CreateAsync(
        OrdenPagoDirectoUpsertDto dto,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var preparedOrder = await PrepareOrderAsync(dto, ct);

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var numeroOrden = await GetNextNumeroOrdenAsync(ct);
        var correlativoProveedor = await ReserveCorrelativoProveedorAsync(preparedOrder.CodigoProveedor, ct);
        var usuarioActual = GetCurrentUser();
        bool? statusTransacc = null;

        await ApplyCompromisoPresupuestoAsync(
            preparedOrder.FechaCompromiso,
            BuildPresupuestoAfectacionLineas(preparedOrder.Detalles),
            direction: 1,
            ct);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO public.prv_compromiso_hdr
               (company_id, numero_orden, correlativo_proveedor, fecha, monto, concepto, cod_proveedor, flag_proveedor, cuenta_contable, cod_proyecto, rtn, pagar_a, status_transacc, nombre_proveedor)
               VALUES
               ({EnsureCompanyId()}, {numeroOrden}, {correlativoProveedor}, {preparedOrder.FechaCompromiso}, {preparedOrder.MontoTotal}, {preparedOrder.Concepto}, {preparedOrder.CodigoProveedor},
                {preparedOrder.FlagProveedor}, {preparedOrder.CuentaContable}, {preparedOrder.CodigoProyecto}, {preparedOrder.Rtn}, {preparedOrder.PagarA}, {statusTransacc}, {preparedOrder.NombreProveedor})",
            ct);

        foreach (var detalle in preparedOrder.Detalles)
        {
            await InsertDetalleAsync(numeroOrden, detalle, ct);
        }

        if (dto.GenerarPartida)
        {
            var lineasContables = await BuildCreatePartidaLineasAsync(preparedOrder, ct);
            var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
            var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
                ?? throw new InvalidOperationException("No se pudo obtener la transaccion activa para registrar la partida contable.");
            await RegisterPartidaContableAsync(
                connection,
                transaction,
                BuildDocumentNumber(numeroOrden),
                BuildPartidaNumber(numeroOrden, "GEN"),
                preparedOrder.FechaCompromiso,
                preparedOrder.Concepto,
                usuarioActual,
                lineasContables,
                ct);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            CorrelativoProveedor = correlativoProveedor,
            Message = BuildCreateSavedMessage(correlativoProveedor)
        };
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> UpdateAsync(
        int numeroOrden,
        OrdenPagoDirectoUpsertDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));
        }

        ArgumentNullException.ThrowIfNull(dto);

        await EnsureEditableAsync(numeroOrden, ct);

        var preparedOrder = await PrepareOrderAsync(dto, ct);

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var snapshot = await LoadCompromisoPresupuestoSnapshotAsync(numeroOrden, ct);
        var correlativoProveedor = await ResolveCorrelativoProveedorForUpdateAsync(numeroOrden, preparedOrder.CodigoProveedor, ct);

        await ApplyCompromisoPresupuestoAsync(
            snapshot.FechaCompromiso,
            BuildPresupuestoAfectacionLineas(snapshot.Detalles),
            direction: -1,
            ct);

        await ApplyCompromisoPresupuestoAsync(
            preparedOrder.FechaCompromiso,
            BuildPresupuestoAfectacionLineas(preparedOrder.Detalles),
            direction: 1,
            ct);

        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE public.prv_compromiso_hdr
               SET correlativo_proveedor = {correlativoProveedor},
                   fecha = {preparedOrder.FechaCompromiso},
                   monto = {preparedOrder.MontoTotal},
                   concepto = {preparedOrder.Concepto},
                   cod_proveedor = {preparedOrder.CodigoProveedor},
                   nombre_proveedor = {preparedOrder.NombreProveedor},
                   flag_proveedor = {preparedOrder.FlagProveedor},
                   cuenta_contable = {preparedOrder.CuentaContable},
                   cod_proyecto = {preparedOrder.CodigoProyecto},
                   rtn = {preparedOrder.Rtn},
                   pagar_a = {preparedOrder.PagarA}
               WHERE numero_orden = {numeroOrden}
                 AND company_id = {EnsureCompanyId()}
                 AND status_transacc IS DISTINCT FROM TRUE",
            ct);

        if (affectedRows == 0)
        {
            throw new InvalidOperationException("La orden no se puede editar porque ya fue procesada.");
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"DELETE FROM public.prv_compromiso_dtl
               WHERE numero_orden = {numeroOrden}
                 AND company_id = {EnsureCompanyId()}",
            ct);

        foreach (var detalle in preparedOrder.Detalles)
        {
            await InsertDetalleAsync(numeroOrden, detalle, ct);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            CorrelativoProveedor = correlativoProveedor,
            Message = BuildSaveMessage(false, correlativoProveedor)
        };
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> DeleteAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
        {
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));
        }

        await EnsureEditableAsync(numeroOrden, ct);

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var snapshot = await LoadCompromisoPresupuestoSnapshotAsync(numeroOrden, ct);

        await ApplyCompromisoPresupuestoAsync(
            snapshot.FechaCompromiso,
            BuildPresupuestoAfectacionLineas(snapshot.Detalles),
            direction: -1,
            ct);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"DELETE FROM public.prv_compromiso_dtl
               WHERE numero_orden = {numeroOrden}
                 AND company_id = {EnsureCompanyId()}",
            ct);

        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"DELETE FROM public.prv_compromiso_hdr
               WHERE numero_orden = {numeroOrden}
                 AND company_id = {EnsureCompanyId()}
                 AND status_transacc IS DISTINCT FROM TRUE",
            ct);

        if (affectedRows == 0)
        {
            throw new InvalidOperationException("La orden no se puede eliminar porque ya fue procesada.");
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            Message = "El compromiso fue eliminado correctamente."
        };
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> MarkAsProcessedAsync(
        int numeroOrden,
        ProcesarOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        var orden = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new { x.numero_orden, x.fecha, x.concepto, x.monto, x.cod_proveedor, x.status_transacc })
            .FirstOrDefaultAsync(ct);

        if (orden is null)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden no existe." };

        if (orden.status_transacc == true)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden ya fue procesada." };

        var faltaPartidaCreacion = !await HasPartidaContableRegistradaAsync(numeroOrden, ct);
        List<PartidaLineaOrden>? lineasCreacion = null;
        if (faltaPartidaCreacion)
        {
            (lineasCreacion, _, _) = await BuildCreatePartidaFromStoredAsync(numeroOrden, ct);
        }

        var fechaOrden = DateTime.SpecifyKind(orden.fecha.Date, DateTimeKind.Utc);
        var descripcion = (orden.concepto?.Trim() ?? $"Orden pago directo {numeroOrden}");
        if (descripcion.Length > 200) descripcion = descripcion[..200];
        var usuarioProceso = string.IsNullOrWhiteSpace(dto.Usuario)
            ? GetCurrentUser()
            : dto.Usuario.Trim();
        // El metodo de pago es opcional: cuando no se especifica, el procesamiento solo
        // genera la partida contable (cuenta del proveedor al credito, cuentas contra al debito)
        // sin transacciones bancarias vinculadas.
        var metodoPago = NormalizeMetodoPago(dto.MetodoPago);

        var lineasDto = dto.Lineas ?? new List<PartidaLineaOrdenPagoDto>();
        IReadOnlyList<PartidaLineaOrdenPagoDto> lineasContraProcesamiento;

        if (lineasDto.Count > 0)
        {
            lineasContraProcesamiento = lineasDto;
        }
        else if (dto.CuentaContraId.HasValue && dto.CuentaContraId.Value > 0)
        {
            lineasContraProcesamiento = new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = dto.CuentaContraId.Value,
                    Descripcion = descripcion,
                    Debito = orden.monto,
                    Credito = 0m
                }
            };
        }
        else
        {
            throw new ArgumentException(
                "Debe seleccionar al menos una cuenta contra para generar la partida de procesamiento.",
                nameof(dto));
        }

        var lineasContraNormalizadas = await NormalizeContraProcessingLinesAsync(
            lineasContraProcesamiento,
            orden.monto,
            descripcion,
            ct);
        var lineas = await BuildProviderProcessingPartidaLineasAsync(
            orden.cod_proveedor,
            lineasContraNormalizadas,
            orden.monto,
            descripcion,
            ct);

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        var kardexIds = new List<long>();

        if (shouldClose)
            await connection.OpenAsync(ct);

        try
        {
            await using var dbTransaction = await connection.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
            if (faltaPartidaCreacion && lineasCreacion is not null)
            {
                await RegisterPartidaContableAsync(
                    connection,
                    dbTransaction,
                    BuildDocumentNumber(numeroOrden),
                    BuildPartidaNumber(numeroOrden, "GEN"),
                    fechaOrden,
                    descripcion,
                    usuarioProceso,
                    lineasCreacion,
                    ct);
            }

            var partidaId = await RegisterPartidaContableAsync(
                connection,
                dbTransaction,
                BuildDocumentNumber(numeroOrden),
                BuildPartidaNumber(numeroOrden, "PRC"),
                fechaOrden,
                descripcion,
                usuarioProceso,
                lineas,
                ct);

            if (OrdenPagoDirectoMetodoPago.EsBancario(metodoPago))
            {
                if (!partidaId.HasValue || partidaId.Value <= 0)
                {
                    throw new InvalidOperationException(
                        "No se pudo resolver la partida contable del procesamiento para vincular la transaccion bancaria.");
                }

                kardexIds = await RegisterLinkedBankTransactionsAsync(
                    connection,
                    dbTransaction,
                    numeroOrden,
                    fechaOrden,
                    descripcion,
                    usuarioProceso,
                    metodoPago,
                    partidaId.Value,
                    lineasContraNormalizadas,
                    ct);
            }

            await UpdateCompromisoStatusTransaccAsync(
                connection,
                dbTransaction,
                EnsureCompanyId(),
                numeroOrden,
                statusTransacc: true,
                ct);

            await dbTransaction.CommitAsync(ct);
        }
        finally
        {
            if (shouldClose)
                await connection.CloseAsync();
        }

        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            Message = BuildProcessSavedMessage(metodoPago, kardexIds.Count)
        };
    }

    private async Task<List<PartidaLineaOrden>> BuildProviderProcessingPartidaLineasAsync(
        string? codigoProveedor,
        long cuentaContraId,
        decimal monto,
        string descripcion,
        CancellationToken ct)
    {
        var lineasContra = await NormalizeContraProcessingLinesAsync(
            new List<PartidaLineaOrdenPagoDto>
            {
                new()
                {
                    CuentaId = cuentaContraId,
                    Descripcion = descripcion,
                    Debito = monto,
                    Credito = 0m
                }
            },
            monto,
            descripcion,
            ct);

        return await BuildProviderProcessingPartidaLineasAsync(
            codigoProveedor,
            lineasContra,
            monto,
            descripcion,
            ct);
    }

    private async Task<List<PartidaLineaOrden>> BuildProviderProcessingPartidaLineasAsync(
        string? codigoProveedor,
        IReadOnlyList<PartidaLineaOrdenPagoDto> lineasContraDto,
        decimal monto,
        string descripcion,
        CancellationToken ct)
    {
        var lineasContra = await NormalizeContraProcessingLinesAsync(
            lineasContraDto,
            monto,
            descripcion,
            ct);

        return await BuildProviderProcessingPartidaLineasAsync(
            codigoProveedor,
            lineasContra,
            monto,
            descripcion,
            ct);
    }

    private async Task<List<PartidaLineaOrden>> BuildProviderProcessingPartidaLineasAsync(
        string? codigoProveedor,
        IReadOnlyList<NormalizedContraProcessingLine> lineasContra,
        decimal monto,
        string descripcion,
        CancellationToken ct)
    {
        if (monto <= 0m)
        {
            throw new InvalidOperationException("El compromiso no tiene un monto valido para generar la partida de procesamiento.");
        }

        if (lineasContra.Count == 0)
        {
            throw new InvalidOperationException("Debe seleccionar al menos una cuenta contra para generar la partida de procesamiento.");
        }

        var proveedor = await LoadProveedorMetadataAsync(codigoProveedor, ct);
        if (proveedor is null)
        {
            throw new InvalidOperationException("No se encontro el proveedor del compromiso.");
        }

        if (string.IsNullOrWhiteSpace(proveedor.CuentaContable))
        {
            throw new InvalidOperationException("El proveedor no tiene una cuenta contable configurada.");
        }

        var companyId = EnsureCompanyId();
        var cuentaProveedorCodigo = proveedor.CuentaContable.Trim();
        var cuentaContraIds = lineasContra
            .Select(x => x.AccountId)
            .Distinct()
            .ToList();

        var cuentas = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(x =>
                x.company_id == companyId &&
                (cuentaContraIds.Contains(x.account_id) || x.code == cuentaProveedorCodigo))
            .Select(x => new
            {
                x.account_id,
                x.code,
                x.name,
                x.allows_posting,
                x.status
            })
            .ToListAsync(ct);

        var cuentaProveedor = cuentas.FirstOrDefault(x =>
            string.Equals(x.code?.Trim(), cuentaProveedorCodigo, StringComparison.OrdinalIgnoreCase));
        if (cuentaProveedor is null)
        {
            throw new InvalidOperationException(
                $"No se encontro la cuenta contable del proveedor ({cuentaProveedorCodigo}) en el plan de cuentas.");
        }

        if (!cuentaProveedor.allows_posting)
        {
            throw new InvalidOperationException("La cuenta contable del proveedor no permite movimientos.");
        }

        if (!IsCuentaActiva(cuentaProveedor.status))
        {
            throw new InvalidOperationException("La cuenta contable del proveedor esta inactiva.");
        }

        var cuentasContraMap = cuentas
            .Where(x => cuentaContraIds.Contains(x.account_id))
            .ToDictionary(x => x.account_id);

        var lineas = new List<PartidaLineaOrden>();
        for (var i = 0; i < lineasContra.Count; i++)
        {
            var lineaContra = lineasContra[i];
            if (!cuentasContraMap.TryGetValue(lineaContra.AccountId, out var cuentaContra))
            {
                throw new InvalidOperationException(
                    $"La cuenta contra de la fila {lineaContra.RowNumber} no existe en la empresa actual.");
            }

            if (!cuentaContra.allows_posting)
            {
                throw new InvalidOperationException(
                    $"La cuenta contra de la fila {lineaContra.RowNumber} no permite movimientos.");
            }

            if (!IsCuentaActiva(cuentaContra.status))
            {
                throw new InvalidOperationException(
                    $"La cuenta contra de la fila {lineaContra.RowNumber} esta inactiva.");
            }

            if (cuentaContra.account_id == cuentaProveedor.account_id)
            {
                throw new InvalidOperationException(
                    $"La cuenta contra de la fila {lineaContra.RowNumber} no puede ser la misma cuenta contable del proveedor.");
            }

            lineas.Add(new PartidaLineaOrden(
                cuentaContra.account_id,
                null,
                lineaContra.Description,
                lineaContra.Debit,
                0m));
        }

        lineas.Add(new PartidaLineaOrden(
            cuentaProveedor.account_id,
            null,
            descripcion,
            0m,
            monto));

        return lineas;
    }

    private async Task<(List<PartidaLineaOrden> Lineas, DateTime FechaCompromiso, string Concepto)>
        BuildCreatePartidaFromStoredAsync(int numeroOrden, CancellationToken ct)
    {
        var detalle = await GetByNumeroOrdenAsync(numeroOrden, ct)
            ?? throw new ArgumentException($"No se encontro el compromiso {numeroOrden}.", nameof(numeroOrden));

        var upsert = new OrdenPagoDirectoUpsertDto
        {
            FechaCompromiso = detalle.FechaCompromiso ?? DateTime.Today,
            CodigoProveedor = detalle.CodigoProveedor,
            Rtn = detalle.Rtn,
            Concepto = detalle.Concepto,
            CuentaContable = detalle.CuentaContable ?? string.Empty,
            PagarA = detalle.PagarA,
            Detalles = detalle.Detalles
                .Select(x => new OrdenPagoDirectoUpsertLineaDto
                {
                    CodigoPresupuestario = string.IsNullOrWhiteSpace(x.CodigoPresupuestario) ? "0" : x.CodigoPresupuestario!,
                    Descripcion = x.Descripcion ?? detalle.Concepto,
                    ConceptoDetalle = x.ConceptoDetalle,
                    Monto = x.Monto,
                    Programa = x.Programa,
                    Actividad = x.Actividad,
                    ObjetoGasto = x.ObjetoGasto,
                    CuentaContable = x.CuentaContable
                })
                .ToList()
        };

        var preparedOrder = await PrepareOrderAsync(upsert, ct);
        var lineas = await BuildCreatePartidaLineasAsync(preparedOrder, ct);
        return (lineas, preparedOrder.FechaCompromiso, preparedOrder.Concepto);
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> GenerarPartidaCreacionAsync(
        int numeroOrden,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        var orden = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new { x.numero_orden, x.status_transacc, x.anulado })
            .FirstOrDefaultAsync(ct);

        if (orden is null)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "El compromiso no existe." };

        if (orden.status_transacc == true)
            throw new InvalidOperationException("El compromiso ya fue procesado.");

        if (orden.anulado)
            throw new InvalidOperationException("El compromiso esta anulado.");

        if (await HasPartidaContableRegistradaAsync(numeroOrden, ct))
            throw new InvalidOperationException("El compromiso ya tiene una partida contable registrada.");

        var (lineas, fechaCompromiso, concepto) = await BuildCreatePartidaFromStoredAsync(numeroOrden, ct);
        var usuarioActual = GetCurrentUser();

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("No se pudo obtener la transaccion activa para registrar la partida contable.");

        await RegisterPartidaContableAsync(
            connection,
            transaction,
            BuildDocumentNumber(numeroOrden),
            BuildPartidaNumber(numeroOrden, "GEN"),
            fechaCompromiso,
            concepto,
            usuarioActual,
            lineas,
            ct);

        await tx.CommitAsync(ct);

        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            Message = "Se genero la partida contable del compromiso."
        };
    }

    private async Task<List<PartidaLineaOrden>> BuildCreatePartidaLineasAsync(
        PreparedOrder preparedOrder,
        CancellationToken ct)
    {
        var cuentasGastoAgrupadas = preparedOrder.Detalles
            .Where(x => !string.IsNullOrWhiteSpace(x.CuentaGasto) && x.Monto > 0m)
            .GroupBy(x => x.CuentaGasto.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new CuentaPartidaAgrupada
            {
                CuentaContable = x.Key,
                Monto = x.Sum(y => y.Monto)
            })
            .OrderBy(x => x.CuentaContable, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (cuentasGastoAgrupadas.Count == 0)
        {
            throw new ArgumentException(
                "No se pudo generar la partida contable del compromiso porque el detalle no tiene cuentas de gasto.",
                nameof(preparedOrder));
        }

        var montoResuelto = cuentasGastoAgrupadas.Sum(x => x.Monto);
        if (Math.Abs(preparedOrder.MontoTotal - montoResuelto) > 0.01m)
        {
            throw new ArgumentException(
                "No se pudo generar la partida contable del compromiso porque todas las lineas deben tener cuenta de gasto.",
                nameof(preparedOrder));
        }

        var companyId = EnsureCompanyId();
        var accountCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(preparedOrder.CuentaContable))
        {
            accountCodes.Add(preparedOrder.CuentaContable.Trim());
        }

        foreach (var grupo in cuentasGastoAgrupadas)
        {
            accountCodes.Add(grupo.CuentaContable);
        }

        var cuentasMap = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(x => x.company_id == companyId && accountCodes.Contains(x.code))
            .Select(x => new { x.code, x.account_id })
            .ToDictionaryAsync(x => x.code.Trim(), x => x.account_id, StringComparer.OrdinalIgnoreCase, ct);

        var descripcion = preparedOrder.Concepto.Length > 200
            ? preparedOrder.Concepto[..200]
            : preparedOrder.Concepto;
        var lineas = new List<PartidaLineaOrden>();

        if (!string.IsNullOrWhiteSpace(preparedOrder.CuentaContable))
        {
            var cuentaServicio = preparedOrder.CuentaContable.Trim();
            if (!cuentasMap.TryGetValue(cuentaServicio, out var cuentaServicioId))
            {
                throw new ArgumentException(
                    $"No se encontro la cuenta de servicio {cuentaServicio} para generar la partida contable.",
                    nameof(preparedOrder));
            }

            lineas.Add(new PartidaLineaOrden(
                cuentaServicioId,
                null,
                descripcion,
                preparedOrder.MontoTotal,
                0m));
        }
        else
        {
            foreach (var grupo in cuentasGastoAgrupadas)
            {
                if (!cuentasMap.TryGetValue(grupo.CuentaContable, out var cuentaGastoId))
                {
                    throw new ArgumentException(
                        $"No se encontro la cuenta de gasto {grupo.CuentaContable} para generar la partida contable.",
                        nameof(preparedOrder));
                }

                lineas.Add(new PartidaLineaOrden(
                    cuentaGastoId,
                    null,
                    descripcion,
                    grupo.Monto,
                    0m));
            }
        }

        foreach (var grupo in cuentasGastoAgrupadas)
        {
            if (!cuentasMap.TryGetValue(grupo.CuentaContable, out var cuentaGastoId))
            {
                throw new ArgumentException(
                    $"No se encontro la cuenta de gasto {grupo.CuentaContable} para generar la partida contable.",
                    nameof(preparedOrder));
            }

            lineas.Add(new PartidaLineaOrden(
                cuentaGastoId,
                null,
                descripcion,
                0m,
                grupo.Monto));
        }

        var totalDebito = lineas.Sum(x => x.Debit);
        var totalCredito = lineas.Sum(x => x.Credit);
        if (Math.Abs(totalDebito - totalCredito) > 0.01m)
        {
            throw new InvalidOperationException(
                $"La partida contable autogenerada no esta cuadrada. Debitos: {totalDebito:N2}, Creditos: {totalCredito:N2}.");
        }

        return lineas;
    }

    private async Task<long?> RegisterPartidaContableAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string documentNumber,
        string partidaNumber,
        DateTime fechaOrden,
        string descripcion,
        string usuario,
        IReadOnlyList<PartidaLineaOrden> lineas,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var fechaOnly = DateOnly.FromDateTime(fechaOrden);
        var periodId = await ResolvePeriodoIdAsync(companyId, fechaOnly, ct);
        var journalId = await ResolveJournalIdAsync(companyId, ct);
        var typeId = await ResolveTipoPartidaIdAsync(companyId, ct);
        var lineasSql = BuildLineasSql(lineas.Count);
        var resolvedPartidaNumber = await GenerateMonthlyPartidaNumberAsync(connection, transaction, companyId, fechaOrden, ct);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = $@"
  CALL public.sp_registrar_partida_contable(
      @p_company_id, @p_journal_id, @p_period_id,
      @p_module, @p_document_type, @p_document_number,
    @p_partida_number, @p_partida_date, @p_description,
    @p_created_by, @p_type_id,
    {lineasSql}
);";
        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_journal_id", NpgsqlDbType.Bigint, journalId);
        command.Parameters.AddWithValue("p_period_id", NpgsqlDbType.Bigint, periodId);
        command.Parameters.AddWithValue("p_module", NpgsqlDbType.Varchar, "PROV");
        command.Parameters.AddWithValue("p_document_type", NpgsqlDbType.Varchar, "OPD");
        command.Parameters.AddWithValue("p_document_number", NpgsqlDbType.Varchar, documentNumber);
        command.Parameters.AddWithValue("p_partida_number", NpgsqlDbType.Varchar, resolvedPartidaNumber);
        command.Parameters.AddWithValue("p_partida_date", NpgsqlDbType.TimestampTz, fechaOrden);
        command.Parameters.AddWithValue("p_description", NpgsqlDbType.Varchar, descripcion);
        command.Parameters.AddWithValue("p_created_by", NpgsqlDbType.Varchar, usuario);
        command.Parameters.AddWithValue("p_type_id", NpgsqlDbType.Bigint, typeId);
        AddPartidaLineaParameters(command, lineas);

        await command.ExecuteNonQueryAsync(ct);

        return await TryResolvePartidaIdAsync(
            connection,
            transaction,
            companyId,
            "PROV",
            "OPD",
            documentNumber,
            resolvedPartidaNumber,
            usuario,
            ct);
    }

    private static async Task UpdateCompromisoStatusTransaccAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        int numeroOrden,
        bool? statusTransacc,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = @"
UPDATE public.prv_compromiso_hdr
SET status_transacc = @status_transacc
WHERE numero_orden = @numero_orden
  AND company_id = @company_id
  AND status_transacc IS DISTINCT FROM @status_transacc;";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.Add(new NpgsqlParameter("status_transacc", NpgsqlDbType.Boolean)
        {
            Value = statusTransacc.HasValue ? statusTransacc.Value : DBNull.Value
        });
        command.Parameters.AddWithValue("numero_orden", NpgsqlDbType.Integer, numeroOrden);

        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<IReadOnlyList<NormalizedContraProcessingLine>> NormalizeContraProcessingLinesAsync(
        IReadOnlyList<PartidaLineaOrdenPagoDto> lineasContraDto,
        decimal monto,
        string descripcionBase,
        CancellationToken ct)
    {
        if (monto <= 0m)
        {
            throw new InvalidOperationException("El compromiso no tiene un monto valido para generar la partida de procesamiento.");
        }

        if (lineasContraDto.Count == 0)
        {
            throw new InvalidOperationException("Debe seleccionar al menos una cuenta contra para generar la partida de procesamiento.");
        }

        if (lineasContraDto.Any(x => x.Credito != 0m))
        {
            throw new InvalidOperationException("Las lineas de contracuenta no deben incluir creditos.");
        }

        if (lineasContraDto.Any(x => x.Debito <= 0m))
        {
            throw new InvalidOperationException("Todas las contracuentas deben tener un monto mayor a cero.");
        }

        var totalDebitoContra = lineasContraDto.Sum(x => x.Debito);
        if (Math.Abs(totalDebitoContra - monto) > 0.01m)
        {
            throw new InvalidOperationException(
                $"La distribucion de contracuentas debe sumar {monto:N2}. Total distribuido: {totalDebitoContra:N2}.");
        }

        var cuentasContraDisponibles = await GetCuentasContraProcesamientoAsync(ct);
        var cuentasContraPorBanco = cuentasContraDisponibles
            .Where(x => x.BancoCuentaId.HasValue && x.BancoCuentaId.Value > 0)
            .GroupBy(x => x.BancoCuentaId!.Value)
            .ToDictionary(x => x.Key, x => x.First());
        var cuentasContraPorCuenta = cuentasContraDisponibles
            .GroupBy(x => x.AccountId)
            .ToDictionary(x => x.Key, x => x.First());

        var lineasNormalizadas = new List<NormalizedContraProcessingLine>();
        for (var i = 0; i < lineasContraDto.Count; i++)
        {
            var linea = lineasContraDto[i];
            CuentaContableLookupDto? cuentaProcesamiento = null;
            var cuentaId = linea.CuentaId;

            if (linea.BancoCuentaId.HasValue && linea.BancoCuentaId.Value > 0)
            {
                if (!cuentasContraPorBanco.TryGetValue(linea.BancoCuentaId.Value, out cuentaProcesamiento))
                {
                    throw new InvalidOperationException(
                        $"La cuenta bancaria de origen en la fila {i + 1} no es valida para procesamiento.");
                }

                cuentaId = cuentaProcesamiento.AccountId;
            }
            else if (cuentaId > 0)
            {
                cuentasContraPorCuenta.TryGetValue(cuentaId, out cuentaProcesamiento);
            }

            if (cuentaId <= 0)
            {
                throw new InvalidOperationException(
                    $"La cuenta contra de la fila {i + 1} debe tener una cuenta contable valida.");
            }

            // La contracuenta puede ser cualquier cuenta del plan contable. Si ademas esta asociada
            // a una cuenta bancaria de procesamiento, se conservan sus datos bancarios; de lo contrario
            // queda como una linea puramente contable (sin transaccion bancaria vinculada).
            lineasNormalizadas.Add(new NormalizedContraProcessingLine
            {
                RowNumber = i + 1,
                AccountId = cuentaId,
                BancoCuentaId = cuentaProcesamiento?.BancoCuentaId,
                TipoCuenta = cuentaProcesamiento?.TipoCuenta,
                Description = string.IsNullOrWhiteSpace(linea.Descripcion)
                    ? descripcionBase
                    : linea.Descripcion.Trim(),
                Debit = linea.Debito
            });
        }

        return lineasNormalizadas;
    }

    private async Task<List<long>> RegisterLinkedBankTransactionsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int numeroOrden,
        DateTime fechaOrden,
        string descripcion,
        string usuario,
        string metodoPago,
        long partidaId,
        IReadOnlyList<NormalizedContraProcessingLine> lineasContra,
        CancellationToken ct)
    {
        if (!OrdenPagoDirectoMetodoPago.EsBancario(metodoPago))
        {
            return new List<long>();
        }

        if (lineasContra.Any(x => !x.BancoCuentaId.HasValue || x.BancoCuentaId.Value <= 0))
        {
            throw new InvalidOperationException(
                $"El metodo de pago {metodoPago} requiere seleccionar una cuenta bancaria de origen en todas las contracuentas.");
        }

        var bancoCuentaIds = lineasContra
            .Select(x => x.BancoCuentaId!.Value)
            .Distinct()
            .OrderBy(x => x)
            .ToArray();

        var cuentasBanco = await LoadProcessingBankAccountsAsync(connection, transaction, bancoCuentaIds, ct);
        if (cuentasBanco.Count != bancoCuentaIds.Length)
        {
            throw new InvalidOperationException(
                "Una o mas cuentas bancarias de origen ya no estan disponibles para registrar la transaccion.");
        }

        if (string.Equals(metodoPago, OrdenPagoDirectoMetodoPago.Cheque, StringComparison.OrdinalIgnoreCase))
        {
            var cuentaNoCheque = cuentasBanco
                .Select(x => x.Value)
                .FirstOrDefault(x => !IsChequeBankAccount(x.TipoCuenta));
            if (cuentaNoCheque is not null)
            {
                throw new InvalidOperationException(
                    $"La cuenta bancaria {cuentaNoCheque.BancoCuentaId} no esta configurada como cuenta de cheques.");
            }
        }

        var tipoTransaccion = await ResolveBankTransactionTypeAsync(metodoPago, ct);
        var gruposPorBanco = lineasContra
            .GroupBy(x => x.BancoCuentaId!.Value)
            .OrderBy(x => x.Key)
            .ToList();

        var kardexIds = new List<long>();
        for (var i = 0; i < gruposPorBanco.Count; i++)
        {
            var grupo = gruposPorBanco[i];
            if (!cuentasBanco.TryGetValue(grupo.Key, out var cuentaBanco))
            {
                throw new InvalidOperationException(
                    $"No se pudo cargar la cuenta bancaria de origen {grupo.Key}.");
            }

            var referencia = BuildProcessingBankReference(numeroOrden, metodoPago, i + 1);
            var descripcionMovimiento = descripcion.Length <= 500 ? descripcion : descripcion[..500];
            var tasaCambio = ResolveProcessingExchangeRate(cuentaBanco);

            var kardexId = await RegisterLinkedBankMovementAsync(
                connection,
                transaction,
                grupo.Key,
                tipoTransaccion.TipoTransaccionId,
                DateOnly.FromDateTime(fechaOrden),
                descripcionMovimiento,
                referencia,
                tasaCambio,
                grupo.Sum(x => x.Debit),
                usuario,
                partidaId,
                numeroOrden,
                ct);

            kardexIds.Add(kardexId);
        }

        return kardexIds;
    }

    private async Task<long> RegisterLinkedBankMovementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long bancoCuentaId,
        string idTipoTransaccion,
        DateOnly fechaMovimiento,
        string descripcion,
        string referencia,
        decimal tasaCambio,
        decimal monto,
        string usuario,
        long partidaId,
        int numeroOrden,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.StoredProcedure;
        command.CommandText = "public.sp_ban_kardex_registrar_movimiento";

        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, EnsureCompanyId());
        command.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
        command.Parameters.AddWithValue("p_movimiento_id", NpgsqlDbType.Bigint, 0);
        command.Parameters.AddWithValue("p_id_tipo_transaccion", NpgsqlDbType.Varchar, idTipoTransaccion);
        command.Parameters.AddWithValue("p_fecha_movimiento", NpgsqlDbType.Date, fechaMovimiento);
        command.Parameters.AddWithValue("p_descripcion", NpgsqlDbType.Varchar, descripcion);
        command.Parameters.Add(new NpgsqlParameter("p_referencia", NpgsqlDbType.Varchar)
        {
            Value = referencia
        });
        command.Parameters.AddWithValue("p_tasa_cambio", NpgsqlDbType.Numeric, tasaCambio);
        command.Parameters.AddWithValue("p_monto", NpgsqlDbType.Numeric, monto);
        command.Parameters.AddWithValue("p_usuario", NpgsqlDbType.Varchar, usuario);

        var kardexParam = new NpgsqlParameter("p_ban_kardex_id", NpgsqlDbType.Bigint)
        {
            Direction = ParameterDirection.Output
        };
        var saldoParam = new NpgsqlParameter("p_saldo_resultante", NpgsqlDbType.Numeric)
        {
            Direction = ParameterDirection.Output
        };

        command.Parameters.Add(kardexParam);
        command.Parameters.Add(saldoParam);

        await command.ExecuteNonQueryAsync(ct);

        var kardexId = kardexParam.Value is DBNull ? 0 : Convert.ToInt64(kardexParam.Value);
        if (kardexId <= 0)
        {
            throw new InvalidOperationException("No fue posible registrar la transaccion bancaria vinculada al compromiso.");
        }

        await LinkCompromisoBankMovementAsync(
            connection,
            transaction,
            kardexId,
            partidaId,
            numeroOrden,
            fechaMovimiento,
            usuario,
            ct);

        return kardexId;
    }

    private async Task<BankTransactionTypeConfig> ResolveBankTransactionTypeAsync(
        string metodoPago,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var query = _context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(x =>
                x.company_id == companyId &&
                x.entra_sale == "S" &&
                (x.estado == null ||
                 x.estado == string.Empty ||
                 x.estado.ToUpper() == "ACTIVE" ||
                 x.estado.ToUpper() == "ACTIVO"));

        IQueryable<SIAD.Core.Entities.ban_tipos_transacciones> filteredQuery = metodoPago.ToUpperInvariant() switch
        {
            OrdenPagoDirectoMetodoPago.Cheque => query.Where(x =>
                (x.emite_cheque != null &&
                 (x.emite_cheque == "S" ||
                  x.emite_cheque == "Y" ||
                  x.emite_cheque == "1" ||
                  x.emite_cheque == "T" ||
                  x.emite_cheque.ToUpper() == "TRUE")) ||
                EF.Functions.ILike(x.nombre, "%CHEQ%") ||
                EF.Functions.ILike(x.tipo_transaccion, "%CHEQ%") ||
                (x.observaciones != null && EF.Functions.ILike(x.observaciones, "%CHEQ%"))),
            OrdenPagoDirectoMetodoPago.Transferencia => query.Where(x =>
                EF.Functions.ILike(x.nombre, "%TRANSFER%") ||
                EF.Functions.ILike(x.tipo_transaccion, "%TRF%") ||
                EF.Functions.ILike(x.tipo_transaccion, "%TRANSFER%") ||
                (x.observaciones != null && EF.Functions.ILike(x.observaciones, "%TRANSFER%")) ||
                (x.destino != null && EF.Functions.ILike(x.destino, "%TRANSFER%"))),
            OrdenPagoDirectoMetodoPago.Deposito => query.Where(x =>
                EF.Functions.ILike(x.nombre, "%DEPOS%") ||
                EF.Functions.ILike(x.tipo_transaccion, "%DEP%") ||
                (x.observaciones != null && EF.Functions.ILike(x.observaciones, "%DEPOS%")) ||
                (x.destino != null && EF.Functions.ILike(x.destino, "%DEPOS%"))),
            _ => throw new InvalidOperationException($"El metodo de pago {metodoPago} no es valido para generar una transaccion bancaria.")
        };

        var tipo = await filteredQuery
            .OrderBy(x => x.tipo_transaccion)
            .Select(x => new BankTransactionTypeConfig
            {
                TipoTransaccionId = x.tipo_transaccion,
                Nombre = x.nombre
            })
            .FirstOrDefaultAsync(ct);

        if (tipo is null)
        {
            throw new InvalidOperationException(
                $"No existe un tipo de transaccion bancaria activo configurado para el metodo de pago {metodoPago}.");
        }

        return tipo;
    }

    private async Task<Dictionary<long, ProcessingBankAccountInfo>> LoadProcessingBankAccountsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IReadOnlyList<long> bancoCuentaIds,
        CancellationToken ct)
    {
        if (bancoCuentaIds.Count == 0)
        {
            return new Dictionary<long, ProcessingBankAccountInfo>();
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT c.banco_cuenta_id,
       c.tipo,
       c.currency_code,
       m.factor
FROM public.ban_cuenta c
LEFT JOIN public.ban_moneda m
       ON m.company_id = c.company_id
      AND upper(coalesce(m.codigo, '')) = upper(coalesce(c.currency_code, ''))
WHERE c.company_id = @company_id
  AND c.banco_cuenta_id = ANY(@banco_cuenta_ids)
  AND c.activo = TRUE;";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, EnsureCompanyId());
        command.Parameters.Add(new NpgsqlParameter("banco_cuenta_ids", NpgsqlDbType.Array | NpgsqlDbType.Bigint)
        {
            Value = bancoCuentaIds.ToArray()
        });

        var cuentas = new Dictionary<long, ProcessingBankAccountInfo>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var bancoCuentaId = reader.GetInt64(0);
            cuentas[bancoCuentaId] = new ProcessingBankAccountInfo
            {
                BancoCuentaId = bancoCuentaId,
                TipoCuenta = reader.IsDBNull(1) ? null : reader.GetString(1),
                CurrencyCode = reader.IsDBNull(2) ? null : reader.GetString(2),
                Factor = reader.IsDBNull(3) ? null : reader.GetDecimal(3)
            };
        }

        return cuentas;
    }

    private async Task LinkCompromisoBankMovementAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long banKardexId,
        long partidaId,
        int numeroOrden,
        DateOnly fechaMovimiento,
        string usuario,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        await LinkPartidaEnKardexAsync(connection, transaction, companyId, banKardexId, partidaId, ct);

        var hasBanMovimiento = await TableExistsAsync(connection, transaction, "ban_movimiento", ct);
        var hasKardexMovimientoId = await ColumnExistsAsync(connection, transaction, "ban_kardex", "movimiento_id", ct);
        var hasMovimientoPrimaryKey = hasBanMovimiento &&
                                      await ColumnExistsAsync(connection, transaction, "ban_movimiento", "movimiento_id", ct);

        if (hasBanMovimiento && hasKardexMovimientoId && hasMovimientoPrimaryKey)
        {
            await using var movimientoCommand = connection.CreateCommand();
            movimientoCommand.Transaction = transaction;
            movimientoCommand.CommandText = @"
UPDATE public.ban_movimiento m
   SET con_poliza_id = @partida_id,
       origen_modulo = @origen_modulo,
       origen_documento_id = @origen_documento_id,
       updated_at = now(),
       updated_by = @updated_by
FROM public.ban_kardex k
WHERE k.company_id = @company_id
  AND k.ban_kardex_id = @ban_kardex_id
  AND m.company_id = k.company_id
  AND m.movimiento_id = k.movimiento_id;";
            movimientoCommand.Parameters.AddWithValue("partida_id", NpgsqlDbType.Bigint, partidaId);
            movimientoCommand.Parameters.AddWithValue("origen_modulo", NpgsqlDbType.Varchar, "PROV_COMPROMISO");
            movimientoCommand.Parameters.AddWithValue("origen_documento_id", NpgsqlDbType.Bigint, numeroOrden);
            movimientoCommand.Parameters.AddWithValue("updated_by", NpgsqlDbType.Varchar, usuario);
            movimientoCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            movimientoCommand.Parameters.AddWithValue("ban_kardex_id", NpgsqlDbType.Bigint, banKardexId);
            await movimientoCommand.ExecuteNonQueryAsync(ct);
        }

        if (await ColumnExistsAsync(connection, transaction, "ban_kardex", "estado_conciliacion", ct))
        {
            await using var kardexCommand = connection.CreateCommand();
            kardexCommand.Transaction = transaction;
            kardexCommand.CommandText = @"
UPDATE public.ban_kardex
   SET estado_conciliacion = 'NOC',
       updated_at = now(),
       updated_by = @updated_by
 WHERE company_id = @company_id
   AND ban_kardex_id = @ban_kardex_id;";
            kardexCommand.Parameters.AddWithValue("updated_by", NpgsqlDbType.Varchar, usuario);
            kardexCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            kardexCommand.Parameters.AddWithValue("ban_kardex_id", NpgsqlDbType.Bigint, banKardexId);
            await kardexCommand.ExecuteNonQueryAsync(ct);
        }
    }

    private static async Task LinkPartidaEnKardexAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        long banKardexId,
        long partidaId,
        CancellationToken ct)
    {
        var hasPolizaId = await ColumnExistsAsync(connection, transaction, "ban_kardex", "poliza_id", ct);
        var hasPartidaCuentaId = await ColumnExistsAsync(connection, transaction, "ban_kardex", "partida_cuenta_id", ct);

        if (!hasPolizaId && !hasPartidaCuentaId)
        {
            return;
        }

        string setClause;
        if (hasPolizaId && hasPartidaCuentaId)
        {
            setClause = "poliza_id = @partida_id, partida_cuenta_id = @partida_id";
        }
        else if (hasPolizaId)
        {
            setClause = "poliza_id = @partida_id";
        }
        else
        {
            setClause = "partida_cuenta_id = @partida_id";
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $@"
UPDATE public.ban_kardex
   SET {setClause}
 WHERE company_id = @company_id
   AND ban_kardex_id = @ban_kardex_id;";
        command.Parameters.AddWithValue("partida_id", NpgsqlDbType.Bigint, partidaId);
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("ban_kardex_id", NpgsqlDbType.Bigint, banKardexId);
        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        string columnName,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT 1
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @table_name
  AND column_name = @column_name
LIMIT 1;";
        command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);
        command.Parameters.AddWithValue("column_name", NpgsqlDbType.Varchar, columnName);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && result is not DBNull;
    }

    private async Task<long?> TryResolvePartidaIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string module,
        string documentType,
        string documentNumber,
        string partidaNumber,
        string createdBy,
        CancellationToken ct)
    {
        if (await TableExistsAsync(connection, transaction, "con_partida_hdr", ct))
        {
            var partidaId = await TryResolvePartidaIdInConPartidaHdrAsync(
                connection,
                transaction,
                companyId,
                module,
                documentType,
                documentNumber,
                partidaNumber,
                createdBy,
                ct);
            if (partidaId.HasValue)
            {
                return partidaId;
            }
        }

        if (await TableExistsAsync(connection, transaction, "con_partida_hdr", ct))
        {
            return await TryResolvePartidaIdInConPolizaAsync(
                connection,
                transaction,
                companyId,
                module,
                documentType,
                documentNumber,
                partidaNumber,
                createdBy,
                ct);
        }

        return null;
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string tableName,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT 1
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @table_name
LIMIT 1;";
        command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);
        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && result is not DBNull;
    }

    private static async Task<long?> TryResolvePartidaIdInConPartidaHdrAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string module,
        string documentType,
        string documentNumber,
        string partidaNumber,
        string createdBy,
        CancellationToken ct)
    {
        const string sql = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = @module
  AND document_type = @document_type
  AND (
        btrim(coalesce(document_number, '')) = btrim(@document_number)
     OR btrim(coalesce(poliza_number, '')) = btrim(@partida_number)
  )
  AND created_by = @created_by
ORDER BY poliza_id DESC
LIMIT 1;";

        var partidaId = await ExecuteScalarPartidaIdAsync(
            connection,
            transaction,
            sql,
            companyId,
            module,
            documentType,
            documentNumber,
            partidaNumber,
            createdBy,
            ct);

        if (partidaId.HasValue)
        {
            return partidaId;
        }

        const string sqlSinUsuario = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = @module
  AND document_type = @document_type
  AND (
        btrim(coalesce(document_number, '')) = btrim(@document_number)
     OR btrim(coalesce(poliza_number, '')) = btrim(@partida_number)
  )
ORDER BY poliza_id DESC
LIMIT 1;";

        return await ExecuteScalarPartidaIdAsync(
            connection,
            transaction,
            sqlSinUsuario,
            companyId,
            module,
            documentType,
            documentNumber,
            partidaNumber,
            null,
            ct);
    }

    private static async Task<long?> TryResolvePartidaIdInConPolizaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        string module,
        string documentType,
        string documentNumber,
        string partidaNumber,
        string createdBy,
        CancellationToken ct)
    {
        const string sql = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = @module
  AND document_type = @document_type
  AND (
        btrim(coalesce(document_number, '')) = btrim(@document_number)
     OR btrim(coalesce(poliza_number, '')) = btrim(@partida_number)
     OR btrim(coalesce(source_reference, '')) = btrim(@document_number)
  )
  AND created_by = @created_by
ORDER BY poliza_id DESC
LIMIT 1;";

        var partidaId = await ExecuteScalarPartidaIdAsync(
            connection,
            transaction,
            sql,
            companyId,
            module,
            documentType,
            documentNumber,
            partidaNumber,
            createdBy,
            ct);

        if (partidaId.HasValue)
        {
            return partidaId;
        }

        const string sqlSinUsuario = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = @module
  AND document_type = @document_type
  AND (
        btrim(coalesce(document_number, '')) = btrim(@document_number)
     OR btrim(coalesce(poliza_number, '')) = btrim(@partida_number)
     OR btrim(coalesce(source_reference, '')) = btrim(@document_number)
  )
ORDER BY poliza_id DESC
LIMIT 1;";

        return await ExecuteScalarPartidaIdAsync(
            connection,
            transaction,
            sqlSinUsuario,
            companyId,
            module,
            documentType,
            documentNumber,
            partidaNumber,
            null,
            ct);
    }

    private static async Task<long?> ExecuteScalarPartidaIdAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        long companyId,
        string module,
        string documentType,
        string documentNumber,
        string partidaNumber,
        string? createdBy,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("module", NpgsqlDbType.Varchar, module);
        command.Parameters.AddWithValue("document_type", NpgsqlDbType.Varchar, documentType);
        command.Parameters.AddWithValue("document_number", NpgsqlDbType.Varchar, documentNumber);
        command.Parameters.AddWithValue("partida_number", NpgsqlDbType.Varchar, partidaNumber);
        if (createdBy is not null)
        {
            command.Parameters.AddWithValue("created_by", NpgsqlDbType.Varchar, createdBy);
        }

        var result = await command.ExecuteScalarAsync(ct);
        return result is null || result is DBNull ? null : Convert.ToInt64(result);
    }

    private static decimal ResolveProcessingExchangeRate(ProcessingBankAccountInfo cuentaBanco)
    {
        var moneda = string.IsNullOrWhiteSpace(cuentaBanco.CurrencyCode)
            ? "HNL"
            : cuentaBanco.CurrencyCode.Trim().ToUpperInvariant();

        if (!string.Equals(moneda, "USD", StringComparison.OrdinalIgnoreCase))
        {
            return 1m;
        }

        if (!cuentaBanco.Factor.HasValue || cuentaBanco.Factor.Value <= 0m)
        {
            throw new InvalidOperationException(
                $"La cuenta bancaria {cuentaBanco.BancoCuentaId} esta en USD y no tiene una tasa de cambio configurada.");
        }

        return Math.Round(cuentaBanco.Factor.Value, 4, MidpointRounding.AwayFromZero);
    }

    private static bool IsChequeBankAccount(string? tipoCuenta)
    {
        return !string.IsNullOrWhiteSpace(tipoCuenta) &&
               tipoCuenta.Contains("CHEQ", StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeMetodoPago(string? metodoPago)
    {
        return string.IsNullOrWhiteSpace(metodoPago)
            ? string.Empty
            : metodoPago.Trim().ToUpperInvariant();
    }

    private static string BuildProcessingBankReference(int numeroOrden, string metodoPago, int secuencia)
    {
        var suffix = metodoPago.ToUpperInvariant() switch
        {
            OrdenPagoDirectoMetodoPago.Cheque => "CHQ",
            OrdenPagoDirectoMetodoPago.Deposito => "DEP",
            OrdenPagoDirectoMetodoPago.Transferencia => "TRF",
            _ => "BNK"
        };

        return $"OPD-{numeroOrden}-{suffix}-{secuencia:00}";
    }

    public async Task<OrdenPagoDirectoOperacionResultadoDto> AnularAsync(
        int numeroOrden,
        AnularOrdenPagoDirectoDto dto,
        CancellationToken ct = default)
    {
        if (numeroOrden <= 0)
            throw new ArgumentException("El numero de orden no es valido.", nameof(numeroOrden));

        ArgumentNullException.ThrowIfNull(dto);

        var orden = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new { x.numero_orden, x.fecha, x.concepto, x.status_transacc, x.anulado })
            .FirstOrDefaultAsync(ct);

        if (orden is null)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden no existe." };

        if (orden.anulado)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden ya fue anulada." };

        if (orden.status_transacc == true)
            return new OrdenPagoDirectoOperacionResultadoDto { Success = false, NumeroOrden = numeroOrden, Message = "La orden no se puede anular porque ya fue procesada." };

        var usuarioActual = GetCurrentUser();
        var fechaAnulacion = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
        var descripcionAnu = $"ANULACION: {(orden.concepto ?? $"Orden {numeroOrden}").Trim()}";
        if (descripcionAnu.Length > 200) descripcionAnu = descripcionAnu[..200];

        await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var snapshot = await LoadCompromisoPresupuestoSnapshotAsync(numeroOrden, ct);

        await ApplyCompromisoPresupuestoAsync(
            snapshot.FechaCompromiso,
            BuildPresupuestoAfectacionLineas(snapshot.Detalles),
            direction: -1,
            ct);

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction
            ?? throw new InvalidOperationException("No se pudo obtener la transaccion activa para registrar la contrapartida.");

        var lineasContrapartida = await LoadPartidaLineasParaReversarAsync(connection, transaction, numeroOrden, ct);
        if (lineasContrapartida.Count > 0)
        {
            await RegisterPartidaContableAsync(
                connection,
                transaction,
                BuildDocumentNumber(numeroOrden),
                BuildPartidaNumber(numeroOrden, "ANU"),
                fechaAnulacion,
                descripcionAnu,
                usuarioActual,
                lineasContrapartida,
                ct);
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE public.prv_compromiso_hdr
                  SET anulado = TRUE
                WHERE numero_orden = {numeroOrden}
                  AND company_id = {EnsureCompanyId()}",
            ct);

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var mensajeContrapartida = lineasContrapartida.Count > 0
            ? " y se genero la contrapartida contable."
            : ".";
        return new OrdenPagoDirectoOperacionResultadoDto
        {
            Success = true,
            NumeroOrden = numeroOrden,
            Message = $"El compromiso fue anulado correctamente{mensajeContrapartida}"
        };
    }

    private async Task<List<PartidaLineaOrden>> LoadPartidaLineasParaReversarAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        int numeroOrden,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var documentNumber = BuildDocumentNumber(numeroOrden);
        var genPartidaNumber = BuildPartidaNumber(numeroOrden, "GEN");

        if (!await TableExistsAsync(connection, transaction, "con_partida_hdr", ct) ||
            !await TableExistsAsync(connection, transaction, "con_partida_dtl", ct))
        {
            return new List<PartidaLineaOrden>();
        }

        await using var headerCmd = connection.CreateCommand();
        headerCmd.Transaction = transaction;
        headerCmd.CommandText = @"
SELECT poliza_id
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND ""module"" = 'PROV'
  AND document_type = 'OPD'
  AND (btrim(coalesce(document_number,'')) = btrim(@document_number)
       OR btrim(coalesce(poliza_number,'')) = btrim(@partida_number))
ORDER BY poliza_id ASC
LIMIT 1;";
        headerCmd.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        headerCmd.Parameters.AddWithValue("document_number", NpgsqlDbType.Varchar, documentNumber);
        headerCmd.Parameters.AddWithValue("partida_number", NpgsqlDbType.Varchar, genPartidaNumber);

        var polizaIdObj = await headerCmd.ExecuteScalarAsync(ct);
        if (polizaIdObj is null or DBNull)
            return new List<PartidaLineaOrden>();

        var polizaId = Convert.ToInt64(polizaIdObj);

        await using var dtlCmd = connection.CreateCommand();
        dtlCmd.Transaction = transaction;
        dtlCmd.CommandText = @"
SELECT account_id, cost_center_id, description, debit_amount, credit_amount
FROM public.con_partida_dtl
WHERE company_id = @company_id
  AND poliza_id = @poliza_id
ORDER BY line_number;";
        dtlCmd.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        dtlCmd.Parameters.AddWithValue("poliza_id", NpgsqlDbType.Bigint, polizaId);

        var lineas = new List<PartidaLineaOrden>();
        await using var reader = await dtlCmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            var accountId = reader.GetInt64(0);
            var costCenterId = reader.IsDBNull(1) ? (long?)null : reader.GetInt64(1);
            var description = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
            var debit = reader.IsDBNull(3) ? 0m : reader.GetDecimal(3);
            var credit = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4);
            // Invertir debito/credito para generar la contrapartida
            lineas.Add(new PartidaLineaOrden(accountId, costCenterId, description, credit, debit));
        }

        return lineas;
    }

    private async Task<bool> EstaMovimientoConciliadoAsync(int numeroOrden, CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var connectionString = _context.Database.GetConnectionString()
            ?? _context.Database.GetDbConnection().ConnectionString;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(ct);

        var tableExists = await TableExistsAsync(connection, null!, "ban_movimiento", ct);
        if (!tableExists) return false;

        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT EXISTS (
    SELECT 1 FROM public.ban_movimiento
     WHERE company_id = @company_id
       AND origen_modulo = 'PROV_COMPROMISO'
       AND origen_documento_id = @numero_orden
       AND conciliado = TRUE
)";
        command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("numero_orden", NpgsqlDbType.Bigint, (long)numeroOrden);

        var result = await command.ExecuteScalarAsync(ct);
        return result is true;
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasContablesAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.allows_posting)
            .OrderBy(c => c.code)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasContraProcesamientoAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();

        return await (
            from cuentaBanco in _context.ban_cuenta.AsNoTracking()
            join cuentaContable in _context.con_plan_cuentas.AsNoTracking()
                on cuentaBanco.cont_account_id equals (long?)cuentaContable.account_id
            join banco in _context.ban_banco.AsNoTracking()
                on cuentaBanco.ban_banco_id equals (long?)banco.ban_banco_id into bancoJoin
            from banco in bancoJoin.DefaultIfEmpty()
            where cuentaBanco.company_id == companyId
                && cuentaBanco.cont_account_id.HasValue
                && cuentaBanco.cont_account_id.Value > 0
                && cuentaBanco.activo
                && cuentaContable.company_id == companyId
                && cuentaContable.allows_posting
                && (
                    cuentaContable.status == null ||
                    cuentaContable.status == string.Empty ||
                    cuentaContable.status.ToUpper() == "ACTIVE" ||
                    cuentaContable.status.ToUpper() == "ACTIVO")
                && (banco == null || (banco.company_id == companyId && banco.activo))
            orderby cuentaContable.code,
                    (banco != null ? banco.nombre : cuentaBanco.banco_nombre),
                    cuentaBanco.numero_cuenta,
                    cuentaBanco.banco_cuenta_id
            select new CuentaContableLookupDto
            {
                AccountId = cuentaContable.account_id,
                BancoCuentaId = cuentaBanco.banco_cuenta_id,
                Code = cuentaContable.code,
                Description = cuentaContable.name,
                BancoNombre = banco != null ? banco.nombre : cuentaBanco.banco_nombre,
                NumeroCuenta = cuentaBanco.numero_cuenta,
                TipoCuenta = cuentaBanco.tipo,
                SaldoActual = cuentaBanco.saldo_actual,
                DisplayText = BuildCuentaContraProcesamientoDisplay(
                    cuentaContable.code,
                    new CuentaContraProcesamientoBancoInfo(
                        cuentaBanco.banco_cuenta_id,
                        banco != null ? banco.nombre : cuentaBanco.banco_nombre,
                        cuentaBanco.numero_cuenta,
                        cuentaBanco.tipo,
                        cuentaBanco.saldo_actual))
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasGastoAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        return await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c =>
                c.company_id == companyId &&
                c.allows_posting &&
                (c.status == null || c.status == string.Empty
                    || c.status.ToUpper() == "ACTIVE"
                    || c.status.ToUpper() == "ACTIVO") &&
                (c.account_type.ToUpper() == "GASTO"
                    || c.account_type.ToUpper() == "GASTOS"
                    || c.account_type.ToUpper() == "EGRESO"
                    || c.account_type.ToUpper() == "EGRESOS"
                    || c.account_type.ToUpper() == "COSTO"
                    || c.account_type.ToUpper() == "COSTOS"
                    || c.account_type.ToUpper() == "INGRESO"
                    || c.account_type.ToUpper() == "INGRESOS"))
            .OrderBy(c => c.code)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
            throw new InvalidOperationException("No se pudo determinar la empresa actual.");
        return companyId;
    }

    private async Task<long> ResolveJournalIdAsync(long companyId, CancellationToken ct)
    {
        var journalId = await _context.con_diarios
            .AsNoTracking()
            .Where(d => d.company_id == companyId && d.is_active && d.code.ToUpper() == "PRV")
            .Select(d => d.journal_id)
            .FirstOrDefaultAsync(ct);

        if (journalId > 0) return journalId;

        journalId = await _context.con_diarios
            .AsNoTracking()
            .Where(d => d.company_id == companyId && d.is_active)
            .OrderBy(d => d.journal_id)
            .Select(d => d.journal_id)
            .FirstOrDefaultAsync(ct);

        if (journalId <= 0)
            throw new InvalidOperationException("No se encontro un diario contable activo.");

        return journalId;
    }

    private async Task<long> ResolvePeriodoIdAsync(long companyId, DateOnly fecha, CancellationToken ct)
    {
        var fechaUtc = DateTime.SpecifyKind(fecha.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);

        var periodId = await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId
                        && p.start_date <= fechaUtc
                        && p.end_date >= fechaUtc
                        && (p.status == "OPEN" || p.status == "ABIERTO"))
            .OrderByDescending(p => p.end_date)
            .Select(p => p.period_id)
            .FirstOrDefaultAsync(ct);

        if (periodId > 0) return periodId;

        periodId = await _context.con_periodo_contables
            .AsNoTracking()
            .Where(p => p.company_id == companyId
                        && p.start_date <= fechaUtc
                        && p.end_date >= fechaUtc)
            .OrderByDescending(p => p.end_date)
            .Select(p => p.period_id)
            .FirstOrDefaultAsync(ct);

        if (periodId <= 0)
            throw new InvalidOperationException("No se encontro un periodo contable valido para la fecha de la orden.");

        return periodId;
    }

    private async Task<long> ResolveTipoPartidaIdAsync(long companyId, CancellationToken ct)
    {
        var typeId = await _context.con_tipo_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.is_default && (t.status == "ACTIVE" || t.status == "ACTIVO"))
            .Select(t => t.type_id)
            .FirstOrDefaultAsync(ct);

        if (typeId > 0) return typeId;

        typeId = await _context.con_tipo_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId && (t.status == "ACTIVE" || t.status == "ACTIVO"))
            .OrderBy(t => t.type_id)
            .Select(t => t.type_id)
            .FirstOrDefaultAsync(ct);

        if (typeId <= 0)
            throw new InvalidOperationException("No hay un tipo de transaccion contable activo configurado.");

        return typeId;
    }

    private static string BuildLineasSql(int count)
    {
        var sb = new StringBuilder();
        sb.Append("ARRAY[");
        for (var i = 0; i < count; i++)
        {
            if (i > 0) sb.Append(", ");
            sb.Append($"ROW(@acc_{i}, @cc_{i}, @desc_{i}, @deb_{i}, @cred_{i}, @third_{i}, @curr_{i}, @rate_{i})");
        }
        sb.Append("]::public.tipo_linea_partida[]");
        return sb.ToString();
    }

    private static void AddPartidaLineaParameters(
        NpgsqlCommand command,
        IReadOnlyList<PartidaLineaOrden> lineas)
    {
        for (var i = 0; i < lineas.Count; i++)
        {
            var l = lineas[i];
            command.Parameters.AddWithValue($"acc_{i}", NpgsqlDbType.Bigint, l.AccountId);
            command.Parameters.Add(new NpgsqlParameter($"cc_{i}", NpgsqlDbType.Bigint)
            {
                Value = l.CostCenterId.HasValue ? l.CostCenterId.Value : DBNull.Value
            });
            command.Parameters.AddWithValue($"desc_{i}", NpgsqlDbType.Varchar, l.Description);
            command.Parameters.AddWithValue($"deb_{i}", NpgsqlDbType.Numeric, l.Debit);
            command.Parameters.AddWithValue($"cred_{i}", NpgsqlDbType.Numeric, l.Credit);
            command.Parameters.Add(new NpgsqlParameter($"third_{i}", NpgsqlDbType.Bigint) { Value = DBNull.Value });
            command.Parameters.AddWithValue($"curr_{i}", NpgsqlDbType.Varchar, "HNL");
            command.Parameters.AddWithValue($"rate_{i}", NpgsqlDbType.Numeric, 1m);
        }
    }

    private sealed record PartidaLineaOrden(
        long AccountId,
        long? CostCenterId,
        string Description,
        decimal Debit,
        decimal Credit);

    private async Task<PreparedOrder> PrepareOrderAsync(OrdenPagoDirectoUpsertDto dto, CancellationToken ct)
    {
        var fechaCompromiso = dto.FechaCompromiso.HasValue
            ? DateTime.SpecifyKind(dto.FechaCompromiso.Value.Date, DateTimeKind.Utc)
            : throw new ArgumentException("La fecha de la orden es requerida.", nameof(dto.FechaCompromiso));

        var codigoProveedor = NormalizeProveedorGenericoCode(
            NormalizeOptional(dto.CodigoProveedor, 20, "codigo del proveedor"));
        if (IsProveedorGenerico(codigoProveedor))
        {
            await _proveedoresService.EnsureProveedorGenericoAsync(ct);
        }

        var proveedor = await LoadProveedorMetadataAsync(codigoProveedor, ct);
        if (codigoProveedor is not null && proveedor is null)
        {
            throw new ArgumentException($"No se encontro el proveedor {codigoProveedor}.", nameof(dto.CodigoProveedor));
        }

        var concepto = NormalizeRequired(dto.Concepto, 150, "El concepto es requerido.");
        var cuentaContable = NormalizeOptional(dto.CuentaContable, 20, "cuenta contable");

        var rtn = NormalizeOptional(dto.Rtn, 20, "RTN")
            ?? NormalizeOptional(proveedor?.Rtn, 20, "RTN");

        var pagarA = NormalizeOptional(dto.PagarA, 100, "pagar a")
            ?? NormalizeOptional(proveedor?.Nombre, 100, "pagar a");

        if (string.IsNullOrWhiteSpace(pagarA))
        {
            throw new ArgumentException("Debe indicar a quien se le pagara la orden.", nameof(dto.PagarA));
        }

        var detalles = await PrepareDetallesAsync(dto.Detalles, ct);
        var tieneCuentaGasto = detalles.Any(x => !string.IsNullOrWhiteSpace(x.CuentaGasto));
        if (string.IsNullOrWhiteSpace(cuentaContable) && !tieneCuentaGasto)
        {
            throw new ArgumentException(
                "Para emitir el compromiso, se debe elegir una cuenta de servicio o de gasto.",
                nameof(dto));
        }

        var montoTotal = detalles.Sum(x => x.Monto);
        if (montoTotal <= 0)
        {
            throw new ArgumentException("La orden debe tener un monto mayor a cero.", nameof(dto.Detalles));
        }

        return new PreparedOrder
        {
            FechaCompromiso = fechaCompromiso,
            CodigoProveedor = codigoProveedor,
            NombreProveedor = ResolveNombreProveedorCompromiso(codigoProveedor, proveedor?.Nombre, pagarA),
            FlagProveedor = string.IsNullOrWhiteSpace(codigoProveedor) ? 0 : 1,
            CuentaContable = cuentaContable ?? string.Empty,
            Rtn = rtn,
            Concepto = concepto,
            PagarA = pagarA,
            CodigoProyecto = null,
            MontoTotal = montoTotal,
            Detalles = detalles
        };
    }

    private async Task<List<PreparedDetailRow>> PrepareDetallesAsync(
        IReadOnlyCollection<OrdenPagoDirectoUpsertLineaDto>? detalles,
        CancellationToken ct)
    {
        if (detalles is null || detalles.Count == 0)
        {
            throw new ArgumentException("Debe registrar al menos una linea presupuestaria.", nameof(detalles));
        }

        var normalizedRows = detalles
            .Select((detalle, index) => NormalizeDetalle(detalle, index))
            .ToList();

        var costCodes = normalizedRows
            .Select(x => x.CodigoPresupuestario)
            .Where(x => !string.Equals(x, "0", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var cuentaGastoCodes = normalizedRows
            .Select(x => x.CuentaContable)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var centroCostoMap = await LoadCentroCostoSaveMapAsync(costCodes, ct);
        var cuentasGastoMap = await LoadCuentasGastoMapAsync(cuentaGastoCodes, ct);

        var prepared = new List<PreparedDetailRow>(normalizedRows.Count);
        foreach (var row in normalizedRows)
        {
            PreparedDetailRow preparedRow;

            if (row.CodigoPresupuestario == "0")
            {
                var cuentaGasto = string.Empty;
                var objetoGasto = string.Empty;

                if (!string.IsNullOrWhiteSpace(row.CuentaContable))
                {
                    if (!cuentasGastoMap.TryGetValue(row.CuentaContable, out var cuentaGastoRow))
                    {
                        throw new ArgumentException(
                            $"La linea {row.NumeroLinea} contiene una cuenta de gasto invalida.",
                            nameof(detalles));
                    }

                    cuentaGasto = cuentaGastoRow.Code;
                    objetoGasto = NormalizeRequired(
                        row.ObjetoGasto ?? cuentaGastoRow.Description ?? row.Descripcion,
                        100,
                        $"La linea {row.NumeroLinea} requiere un objeto de gasto valido.");
                }

                preparedRow = new PreparedDetailRow
                {
                    CodigoPresupuestario = "0",
                    Programa = string.Empty,
                    Actividad = string.Empty,
                    ObjetoGasto = objetoGasto,
                    CuentaGasto = cuentaGasto,
                    Descripcion = row.Descripcion,
                    Monto = row.Monto,
                    ConceptoDetalle = row.ConceptoDetalle
                };
            }
            else
            {
                if (!centroCostoMap.TryGetValue(row.CodigoPresupuestario, out var centroCosto))
                {
                    throw new ArgumentException(
                        $"No existe el codigo presupuestario {row.CodigoPresupuestario}.",
                        nameof(detalles));
                }

                preparedRow = new PreparedDetailRow
                {
                    CodigoPresupuestario = row.CodigoPresupuestario,
                    Programa = NormalizeRequired(
                        centroCosto.ProgramaCodigo,
                        2,
                        $"El codigo de programa del centro {row.CodigoPresupuestario} no es valido."),
                    Actividad = NormalizeRequired(
                        centroCosto.ActividadCodigo,
                        2,
                        $"El codigo de actividad del centro {row.CodigoPresupuestario} no es valido."),
                    ObjetoGasto = NormalizeRequired(
                        row.ObjetoGasto ?? centroCosto.ObjetoGasto,
                        100,
                        $"El centro {row.CodigoPresupuestario} no tiene objeto de gasto valido."),
                    CuentaGasto = NormalizeRequired(
                        row.CuentaContable ?? centroCosto.CuentaContable,
                        20,
                        $"El centro {row.CodigoPresupuestario} no tiene cuenta contable valida."),
                    Descripcion = row.Descripcion,
                    Monto = row.Monto,
                    ConceptoDetalle = row.ConceptoDetalle
                };
            }

            prepared.Add(preparedRow);
        }

        return prepared;
    }

    private static NormalizedDetailRow NormalizeDetalle(OrdenPagoDirectoUpsertLineaDto detalle, int index)
    {
        if (detalle is null)
        {
            throw new ArgumentException($"La linea {index + 1} es invalida.");
        }

        var codigoPresupuestario = NormalizeRequired(
            detalle.CodigoPresupuestario,
            20,
            $"La linea {index + 1} requiere un codigo presupuestario.");

        var descripcion = NormalizeRequired(
            detalle.Descripcion,
            150,
            $"La linea {index + 1} requiere una descripcion.");

        if (detalle.Monto <= 0)
        {
            throw new ArgumentException($"La linea {index + 1} debe tener un monto mayor a cero.");
        }

        return new NormalizedDetailRow
        {
            NumeroLinea = index + 1,
            CodigoPresupuestario = codigoPresupuestario,
            Descripcion = descripcion,
            ConceptoDetalle = NormalizeOptional(detalle.ConceptoDetalle, 100, "concepto detalle"),
            Monto = detalle.Monto,
            ObjetoGasto = NormalizeOptional(detalle.ObjetoGasto, 100, "objeto de gasto"),
            CuentaContable = NormalizeOptional(detalle.CuentaContable, 20, "cuenta de gasto")
        };
    }

    private async Task InsertDetalleAsync(int numeroOrden, PreparedDetailRow detalle, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO public.prv_compromiso_dtl
               (company_id, numero_orden, cod_presupuestario, programa, actividad, objeto_gasto, cuenta_gasto, descripcion, monto, conceptodtl)
               VALUES
               ({EnsureCompanyId()}, {numeroOrden}, {detalle.CodigoPresupuestario}, {detalle.Programa}, {detalle.Actividad}, {detalle.ObjetoGasto},
               {detalle.CuentaGasto}, {detalle.Descripcion}, {detalle.Monto}, {detalle.ConceptoDetalle})",
            ct);
    }

    private static IReadOnlyList<PresupuestoAfectacionLinea> BuildPresupuestoAfectacionLineas(
        IReadOnlyCollection<PreparedDetailRow> detalles)
    {
        return detalles
            .Where(x => !string.IsNullOrWhiteSpace(x.CuentaGasto) && x.Monto > 0m)
            .GroupBy(x => x.CuentaGasto.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => new PresupuestoAfectacionLinea(g.Key, g.Sum(x => x.Monto)))
            .ToList();
    }

    private static IReadOnlyList<PresupuestoAfectacionLinea> BuildPresupuestoAfectacionLineas(
        IReadOnlyCollection<CompromisoPresupuestoDetalleSnapshot> detalles)
    {
        return detalles
            .Where(x => x.Monto > 0m)
            .Select(x => new
            {
                CuentaGasto = x.CuentaGasto?.Trim(),
                x.Monto
            })
            .Where(x => !string.IsNullOrWhiteSpace(x.CuentaGasto))
            .GroupBy(x => x.CuentaGasto!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new PresupuestoAfectacionLinea(g.Key, g.Sum(x => x.Monto)))
            .ToList();
    }

    private async Task ApplyCompromisoPresupuestoAsync(
        DateTime fechaCompromiso,
        IReadOnlyCollection<PresupuestoAfectacionLinea> lineas,
        int direction,
        CancellationToken ct)
    {
        if (lineas.Count == 0)
        {
            return;
        }

        var fechaPresupuesto = DateOnly.FromDateTime(fechaCompromiso);
        var affectedBudgetIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var cuentasPresupuestables = await LoadBudgetEnabledAccountCodesAsync(ct);

        foreach (var linea in lineas)
        {
            var cuentaContable = linea.CuentaContable.Trim();
            var delta = direction * linea.Monto;

            if (string.IsNullOrWhiteSpace(cuentaContable) || delta == 0m)
            {
                continue;
            }

            if (!cuentasPresupuestables.Contains(cuentaContable))
            {
                continue;
            }

            var budgetRow = await FindBudgetDetailForCompromisoAsync(
                cuentaContable,
                fechaPresupuesto,
                requireApproved: delta > 0m,
                ct);

            if (budgetRow is null)
            {
                throw new InvalidOperationException(
                    delta > 0m
                        ? $"No existe un presupuesto aprobado y vigente para la cuenta contable {cuentaContable} en la fecha {fechaPresupuesto:yyyy-MM-dd}."
                        : $"No se encontro el presupuesto a liberar para la cuenta contable {cuentaContable} en la fecha {fechaPresupuesto:yyyy-MM-dd}.");
            }

            var nuevoValorReal = budgetRow.Detail.valor_real + delta;
            if (delta > 0m && nuevoValorReal > budgetRow.Detail.valor_proyeccion)
            {
                var disponibleCuenta = Math.Max(budgetRow.Detail.valor_proyeccion - budgetRow.Detail.valor_real, 0m);
                throw new InvalidOperationException(
                    $"El compromiso excede el presupuesto disponible para la cuenta contable {cuentaContable}. Disponible: {disponibleCuenta:N2}.");
            }

            budgetRow.Detail.valor_real = nuevoValorReal < 0m ? 0m : nuevoValorReal;
            budgetRow.Detail.valor_disponible = Math.Max(
                budgetRow.Detail.valor_proyeccion - budgetRow.Detail.valor_real,
                0m);
            affectedBudgetIds.Add(budgetRow.Header.id_presupuesto);
        }

        await RecalculateBudgetHeadersAsync(affectedBudgetIds, ct);
    }

    private async Task<HashSet<string>> LoadBudgetEnabledAccountCodesAsync(CancellationToken ct)
    {
        var companyId = EnsureCompanyId();

        var codes = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.allows_budget)
            .Select(x => x.code)
            .ToListAsync(ct);

        return codes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task<BudgetDetailAffectRow?> FindBudgetDetailForCompromisoAsync(
        string cuentaContable,
        DateOnly fechaCompromiso,
        bool requireApproved,
        CancellationToken ct)
    {
        var cuentaNormalizada = cuentaContable.Trim().ToUpperInvariant();

        var query =
            from detail in _context.pst_config_presupuesto_dtls
            join header in _context.pst_config_presupuesto_hdrs
                on detail.id_presupuesto equals header.id_presupuesto
            where detail.con_cuenta_code.ToUpper() == cuentaNormalizada &&
                  header.fecha_inicia <= fechaCompromiso &&
                  header.fecha_finaliza >= fechaCompromiso &&
                  (!requireApproved || header.estado_aprobado)
            orderby header.fecha_inicia descending, header.id_presupuesto descending
            select new BudgetDetailAffectRow
            {
                Header = header,
                Detail = detail
            };

        return await query.FirstOrDefaultAsync(ct);
    }

    private async Task RecalculateBudgetHeadersAsync(
        IReadOnlyCollection<string> budgetIds,
        CancellationToken ct)
    {
        if (budgetIds.Count == 0)
        {
            return;
        }

        var headers = await _context.pst_config_presupuesto_hdrs
            .Where(x => budgetIds.Contains(x.id_presupuesto))
            .ToListAsync(ct);

        var details = await _context.pst_config_presupuesto_dtls
            .Where(x => budgetIds.Contains(x.id_presupuesto))
            .ToListAsync(ct);

        var totalsByBudget = details
            .GroupBy(x => x.id_presupuesto, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Sum(x => x.valor_real),
                StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            totalsByBudget.TryGetValue(header.id_presupuesto, out var totalReal);
            header.valor_disponible = Math.Max(header.valor_global - totalReal, 0m);
        }
    }

    private async Task<CompromisoPresupuestoSnapshot> LoadCompromisoPresupuestoSnapshotAsync(
        int numeroOrden,
        CancellationToken ct)
    {
        var header = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new
            {
                x.fecha,
                x.status_transacc
            })
            .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            throw new KeyNotFoundException($"No se encontro el compromiso {numeroOrden}.");
        }

        if (header.status_transacc == true)
        {
            throw new InvalidOperationException("La orden no se puede modificar porque ya fue procesada.");
        }

        var detalles = await _context.prv_compromiso_dtls
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new CompromisoPresupuestoDetalleSnapshot
            {
                CuentaGasto = x.cuenta_gasto,
                Monto = x.monto
            })
            .ToListAsync(ct);

        return new CompromisoPresupuestoSnapshot
        {
            FechaCompromiso = header.fecha,
            Detalles = detalles
        };
    }

    private async Task<int> GetNextNumeroOrdenAsync(CancellationToken ct)
    {
        var currentMax = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Select(x => (int?)x.numero_orden)
            .MaxAsync(ct);

        return (currentMax ?? 0) + 1;
    }

    private static async Task ApplyPresupuestoPartidaAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        DateOnly fechaMovimiento,
        IReadOnlyList<PartidaLineaOrden> lineas,
        CancellationToken ct)
    {
        if (lineas.Count == 0)
        {
            return;
        }

        if (!await SupportsPresupuestoPartidaProcedureAsync(connection, transaction, ct))
        {
            throw new InvalidOperationException(
                "No se encontro el procedimiento public.sp_pst_aplicar_partida_presupuesto. Aplique el script de presupuesto sin disparadores antes de procesar el compromiso.");
        }

        var lineasSql = BuildLineasSql(lineas.Count);

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandType = CommandType.Text;
        command.CommandText = $@"
CALL public.sp_pst_aplicar_partida_presupuesto(
    @p_company_id,
    @p_poliza_date,
    {lineasSql}
);";
        command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
        command.Parameters.AddWithValue("p_poliza_date", NpgsqlDbType.Date, fechaMovimiento);
        AddPartidaLineaParameters(command, lineas);

        await command.ExecuteNonQueryAsync(ct);
    }

    private static async Task<bool> SupportsPresupuestoPartidaProcedureAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
SELECT 1
FROM pg_proc p
JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public'
  AND p.proname = 'sp_pst_aplicar_partida_presupuesto'
LIMIT 1;";

        var result = await command.ExecuteScalarAsync(ct);
        return result is not null && result is not DBNull;
    }

    private async Task<int?> ReserveCorrelativoProveedorAsync(string? codigoProveedor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigoProveedor))
        {
            return null;
        }

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            var companyId = checked((int)EnsureCompanyId());
            await using var command = connection.CreateCommand();
            command.Transaction = _context.Database.CurrentTransaction?.GetDbTransaction() as NpgsqlTransaction;
            command.CommandText = @"
UPDATE public.prv_proveedores
SET ultimo_correlativo_compromiso = COALESCE(ultimo_correlativo_compromiso, 0) + 1
WHERE btrim(cod_proveedor) = btrim(@cod_proveedor)
  AND company_id = @company_id
RETURNING ultimo_correlativo_compromiso;";
            command.Parameters.AddWithValue("cod_proveedor", NpgsqlDbType.Varchar, codigoProveedor);
            command.Parameters.AddWithValue("company_id", NpgsqlDbType.Integer, companyId);

            var result = await command.ExecuteScalarAsync(ct);
            if (result is null || result is DBNull)
            {
                throw new ArgumentException($"No se encontro el proveedor {codigoProveedor}.", nameof(codigoProveedor));
            }

            return result switch
            {
                int correlativo => correlativo,
                long correlativo => checked((int)correlativo),
                short correlativo => correlativo,
                _ => Convert.ToInt32(result)
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<int?> ResolveCorrelativoProveedorForUpdateAsync(
        int numeroOrden,
        string? codigoProveedor,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigoProveedor))
        {
            return null;
        }

        var header = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => new HeaderIdentityRow
            {
                CodigoProveedor = x.cod_proveedor,
                CorrelativoProveedor = x.correlativo_proveedor
            })
            .FirstOrDefaultAsync(ct);

        if (header is null)
        {
            throw new KeyNotFoundException($"No se encontro el compromiso {numeroOrden}.");
        }

        if (SameProviderCode(header.CodigoProveedor, codigoProveedor) &&
            header.CorrelativoProveedor.HasValue &&
            header.CorrelativoProveedor.Value > 0)
        {
            return header.CorrelativoProveedor.Value;
        }

        return await ReserveCorrelativoProveedorAsync(codigoProveedor, ct);
    }

    private async Task EnsureEditableAsync(int numeroOrden, CancellationToken ct)
    {
        var statusRows = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => x.status_transacc)
            .ToListAsync(ct);

        if (statusRows.Count == 0)
        {
            throw new KeyNotFoundException($"No se encontro la orden {numeroOrden}.");
        }

        if (statusRows.Any(x => x == true))
        {
            throw new InvalidOperationException("La orden no se puede modificar porque ya fue procesada.");
        }

        var anulada = await _context.prv_compromiso_hdrs
            .AsNoTracking()
            .Where(x => x.numero_orden == numeroOrden)
            .Select(x => x.anulado)
            .FirstOrDefaultAsync(ct);

        if (anulada)
        {
            throw new InvalidOperationException("La orden no se puede modificar porque ya fue anulada.");
        }

        if (await HasPartidaContableRegistradaAsync(numeroOrden, ct))
        {
            throw new InvalidOperationException("La orden no se puede modificar porque ya tiene una partida contable registrada.");
        }
    }

    private async Task<bool> HasPartidaContableRegistradaAsync(int numeroOrden, CancellationToken ct)
    {
        return await LoadPartidaContableAsync(numeroOrden, ct) is not null;
    }

    private static void ValidateFilter(OrdenPagoDirectoFilterDto? filtro)
    {
        if (filtro is null)
        {
            return;
        }

        if (filtro.NumeroOrden.HasValue && filtro.NumeroOrden.Value <= 0)
        {
            throw new InvalidOperationException("El numero de orden debe ser mayor a cero.");
        }
    }

    private async Task<Dictionary<string, string>> LoadProviderMapAsync(
        IReadOnlyCollection<HeaderRow> headers,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var providerCodes = headers
            .Select(x => x.CodigoProveedor?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (providerCodes.Length == 0)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        return await _context.prv_proveedores
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.cod_proveedor != null && providerCodes.Contains(x.cod_proveedor))
            .Select(x => new
            {
                Codigo = x.cod_proveedor!,
                Nombre = x.nombre
            })
            .ToDictionaryAsync(
                x => x.Codigo,
                x => x.Nombre ?? string.Empty,
                StringComparer.OrdinalIgnoreCase,
                ct);
    }

    private async Task<Dictionary<int, int>> LoadDetailCountMapAsync(
        IReadOnlyCollection<HeaderRow> headers,
        CancellationToken ct)
    {
        var orderIds = headers
            .Select(x => x.NumeroOrden)
            .Distinct()
            .ToArray();

        if (orderIds.Length == 0)
        {
            return new Dictionary<int, int>();
        }

        var detailCounts = await _context.prv_compromiso_dtls
            .AsNoTracking()
            .Where(x => orderIds.Contains(x.numero_orden))
            .GroupBy(x => x.numero_orden)
            .Select(g => new
            {
                NumeroOrden = g.Key,
                TotalDetalles = g.Count()
            })
            .ToListAsync(ct);

        return detailCounts.ToDictionary(x => x.NumeroOrden, x => x.TotalDetalles);
    }

    private async Task<Dictionary<string, CentroCostoDisplayRow>> LoadCentroCostoDisplayMapAsync(
        IReadOnlyCollection<DetailRow> details,
        CancellationToken ct)
    {
        var costCodes = details
            .Select(x => x.CodigoPresupuestario?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (costCodes.Length == 0)
        {
            return new Dictionary<string, CentroCostoDisplayRow>(StringComparer.OrdinalIgnoreCase);
        }

        var centroCostoRows = await (
            from hdr in _context.cnt_centrocostos_hdrs.AsNoTracking()
            join sub in _context.cnt_centro_costos_subgrupos.AsNoTracking()
                on new { hdr.codccg, hdr.codsccg } equals new { sub.codccg, sub.codsccg } into subGroup
            from sub in subGroup.DefaultIfEmpty()
            join grp in _context.cnt_centro_costos_grupos.AsNoTracking()
                on hdr.codccg equals grp.codccg into groupGroup
            from grp in groupGroup.DefaultIfEmpty()
            where costCodes.Contains(hdr.cuenta)
            select new CentroCostoDisplayRow
            {
                CodigoCosto = hdr.cuenta,
                ActividadNombre = sub != null ? sub.nombre : null,
                ProgramaNombre = grp != null ? grp.nombre : null,
                ObjetoGasto = hdr.nombre,
                CuentaContable = hdr.contable
            })
            .ToListAsync(ct);

        return centroCostoRows.ToDictionary(
            x => x.CodigoCosto,
            x => x,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, CentroCostoSaveRow>> LoadCentroCostoSaveMapAsync(
        IReadOnlyCollection<string> costCodes,
        CancellationToken ct)
    {
        if (costCodes.Count == 0)
        {
            return new Dictionary<string, CentroCostoSaveRow>(StringComparer.OrdinalIgnoreCase);
        }

        var centroCostoRows = await _context.cnt_centrocostos_hdrs
            .AsNoTracking()
            .Where(x => costCodes.Contains(x.cuenta))
            .Select(x => new CentroCostoSaveRow
            {
                CodigoCosto = x.cuenta,
                ProgramaCodigo = x.codccg,
                ActividadCodigo = x.codsccg,
                ObjetoGasto = x.nombre,
                CuentaContable = x.contable
            })
            .ToListAsync(ct);

        return centroCostoRows.ToDictionary(
            x => x.CodigoCosto,
            x => x,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<Dictionary<string, CuentaGastoSaveRow>> LoadCuentasGastoMapAsync(
        IReadOnlyCollection<string?> accountCodes,
        CancellationToken ct)
    {
        var normalizedCodes = accountCodes
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (normalizedCodes.Length == 0)
        {
            return new Dictionary<string, CuentaGastoSaveRow>(StringComparer.OrdinalIgnoreCase);
        }

        var companyId = EnsureCompanyId();
        var rows = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c =>
                c.company_id == companyId &&
                normalizedCodes.Contains(c.code) &&
                c.allows_posting &&
                (c.status == null || c.status == string.Empty
                    || c.status.ToUpper() == "ACTIVE"
                    || c.status.ToUpper() == "ACTIVO") &&
                (c.account_type.ToUpper() == "GASTO"
                    || c.account_type.ToUpper() == "GASTOS"
                    || c.account_type.ToUpper() == "EGRESO"
                    || c.account_type.ToUpper() == "EGRESOS"
                    || c.account_type.ToUpper() == "COSTO"
                    || c.account_type.ToUpper() == "COSTOS"
                    || c.account_type.ToUpper() == "INGRESO"
                    || c.account_type.ToUpper() == "INGRESOS"))
            .Select(c => new CuentaGastoSaveRow
            {
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);

        return rows.ToDictionary(
            x => x.Code,
            x => x,
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<ProveedorMetadata?> LoadProveedorMetadataAsync(string? codigoProveedor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(codigoProveedor))
        {
            return null;
        }

        var companyId = EnsureCompanyId();
        return await _context.prv_proveedores
            .AsNoTracking()
            .Where(x => x.company_id == companyId && x.cod_proveedor == codigoProveedor)
            .Select(x => new ProveedorMetadata
            {
                CodigoProveedor = x.cod_proveedor,
                Nombre = x.nombre,
                Rtn = x.rtn,
                CuentaContable = x.cuenta_contable
            })
            .FirstOrDefaultAsync(ct);
    }

    private static string ResolveProveedor(
        IReadOnlyDictionary<string, string> providerMap,
        string? codigoProveedor,
        string? nombreProveedor,
        string? pagarA)
    {
        if (!string.IsNullOrWhiteSpace(nombreProveedor))
        {
            return nombreProveedor.Trim();
        }

        if (!string.IsNullOrWhiteSpace(codigoProveedor) &&
            providerMap.TryGetValue(codigoProveedor.Trim(), out var providerName) &&
            !string.IsNullOrWhiteSpace(providerName))
        {
            return providerName.Trim();
        }

        return string.IsNullOrWhiteSpace(pagarA) ? string.Empty : pagarA.Trim();
    }

    private static string ResolveNombreProveedorCompromiso(
        string? codigoProveedor,
        string? nombreProveedorCatalogo,
        string pagarA)
    {
        if (IsProveedorGenerico(codigoProveedor))
        {
            return pagarA;
        }

        return string.IsNullOrWhiteSpace(nombreProveedorCatalogo)
            ? pagarA
            : nombreProveedorCatalogo.Trim();
    }

    private static string? NormalizeProveedorGenericoCode(string? codigoProveedor)
    {
        return IsProveedorGenerico(codigoProveedor)
            ? ProveedoresConstants.CodigoProveedorGenericoCompromisos
            : codigoProveedor?.Trim();
    }

    private static bool IsProveedorGenerico(string? codigoProveedor)
    {
        var normalizado = codigoProveedor?.Trim();

        return string.Equals(
                   normalizado,
                   ProveedoresConstants.CodigoProveedorGenericoCompromisos,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizado,
                   ProveedoresConstants.CodigoProveedorGenericoCompromisosLegacy,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<OrdenPagoDirectoListItemDto> ApplyFilter(
        IReadOnlyCollection<OrdenPagoDirectoListItemDto> items,
        OrdenPagoDirectoFilterDto? filtro)
    {
        IEnumerable<OrdenPagoDirectoListItemDto> query = items;

        if (filtro?.NumeroOrden is > 0)
        {
            query = query.Where(x => x.NumeroOrden == filtro.NumeroOrden.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro?.CodigoProveedor))
        {
            var codigoProveedor = filtro.CodigoProveedor.Trim();
            query = query.Where(x =>
                !string.IsNullOrWhiteSpace(x.CodigoProveedor) &&
                string.Equals(x.CodigoProveedor.Trim(), codigoProveedor, StringComparison.OrdinalIgnoreCase));
        }

        if (string.IsNullOrWhiteSpace(filtro?.Search))
        {
            return query;
        }

        var search = filtro.Search.Trim();
        return query.Where(x =>
            (x.CorrelativoProveedor.HasValue &&
             x.CorrelativoProveedor.Value.ToString().Contains(search, StringComparison.OrdinalIgnoreCase)) ||
            ContainsIgnoreCase(x.Proveedor, search) ||
            ContainsIgnoreCase(x.Rtn, search) ||
            ContainsIgnoreCase(x.Concepto, search) ||
            ContainsIgnoreCase(x.CuentaContable, search) ||
            ContainsIgnoreCase(x.CodigoProveedor, search) ||
            ContainsIgnoreCase(x.PagarA, search));
    }

    private async Task<OrdenPagoDirectoPartidaContableDto?> LoadPartidaContableAsync(
        int numeroOrden,
        CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        if (await TableExistsAsync("con_partida_hdr", ct) && await TableExistsAsync("con_partida_dtl", ct))
        {
            var partidaConPartida = await LoadPartidaContableFromConPartidaAsync(companyId, numeroOrden, ct);
            if (partidaConPartida is not null)
            {
                return partidaConPartida;
            }
        }

        if (await TableExistsAsync("con_partida_hdr", ct) && await TableExistsAsync("con_partida_dtl", ct))
        {
            var partidaConPoliza = await LoadPartidaContableFromConPolizaAsync(companyId, numeroOrden, ct);
            if (partidaConPoliza is not null)
            {
                return partidaConPoliza;
            }
        }

        if (companyId <= int.MaxValue &&
            await TableExistsAsync("cnt_partidas_hdr", ct) &&
            await TableExistsAsync("cnt_partidas_dtl", ct))
        {
            return await LoadPartidaContableFromCntPartidasAsync((int)companyId, numeroOrden, ct);
        }

        return null;
    }

    private async Task<OrdenPagoDirectoPartidaContableDto?> LoadPartidaContableFromConPartidaAsync(
        long companyId,
        int numeroOrden,
        CancellationToken ct)
    {
        var documentNumber = $"OPD-{numeroOrden}";
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var headerCommand = connection.CreateCommand();
            headerCommand.CommandText = @"
SELECT h.poliza_id,
       h.poliza_number,
       h.poliza_date,
       h.description
FROM public.con_partida_hdr h
WHERE h.company_id = @company_id
  AND h.""module"" = 'PROV'
  AND h.document_type = 'OPD'
  AND (
        btrim(coalesce(h.document_number, '')) = btrim(@document_number)
     OR btrim(coalesce(h.poliza_number, '')) = btrim(@document_number)
     OR EXISTS (
            SELECT 1
            FROM public.con_partida_dtl d
            WHERE d.company_id = h.company_id
              AND d.poliza_id = h.poliza_id
              AND btrim(coalesce(d.source_document, '')) = btrim(@document_number)
        )
  )
ORDER BY h.poliza_id DESC
LIMIT 1;";
            headerCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            headerCommand.Parameters.AddWithValue("document_number", NpgsqlDbType.Varchar, documentNumber);

            long polizaId;
            string numeroPartida;
            DateTime fechaPartida;
            string? descripcion;

            await using (var reader = await headerCommand.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                polizaId = reader.GetInt64(0);
                numeroPartida = reader.IsDBNull(1) ? documentNumber : reader.GetString(1);
                fechaPartida = reader.GetDateTime(2);
                descripcion = reader.IsDBNull(3) ? null : reader.GetString(3);
            }

            await using var lineasCommand = connection.CreateCommand();
            lineasCommand.CommandText = @"
SELECT COALESCE(a.code, ''),
       a.name,
       CASE
           WHEN cc.cost_center_id IS NULL THEN NULL
           ELSE cc.code || ' - ' || cc.name
       END AS centro_costo,
       d.description,
       d.debit_amount,
       d.credit_amount
FROM public.con_partida_dtl d
LEFT JOIN public.con_plan_cuentas a
       ON a.company_id = d.company_id
      AND a.account_id = d.account_id
LEFT JOIN public.con_centro_costo cc
       ON cc.company_id = d.company_id
      AND cc.cost_center_id = d.cost_center_id
WHERE d.company_id = @company_id
  AND d.poliza_id = @poliza_id
ORDER BY d.line_number;";
            lineasCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            lineasCommand.Parameters.AddWithValue("poliza_id", NpgsqlDbType.Bigint, polizaId);

            var lineas = new List<OrdenPagoDirectoPartidaContableLineaDto>();
            await using (var reader = await lineasCommand.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    lineas.Add(new OrdenPagoDirectoPartidaContableLineaDto
                    {
                        CuentaContable = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        NombreCuenta = reader.IsDBNull(1) ? null : reader.GetString(1),
                        CentroCosto = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Debito = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                        Credito = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5)
                    });
                }
            }

            return new OrdenPagoDirectoPartidaContableDto
            {
                NumeroPartida = numeroPartida,
                FechaPartida = fechaPartida,
                Descripcion = descripcion,
                Lineas = lineas
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<OrdenPagoDirectoPartidaContableDto?> LoadPartidaContableFromConPolizaAsync(
        long companyId,
        int numeroOrden,
        CancellationToken ct)
    {
        var documentNumber = $"OPD-{numeroOrden}";
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var headerCommand = connection.CreateCommand();
            headerCommand.CommandText = @"
SELECT h.poliza_id,
       h.poliza_number,
       h.poliza_date,
       h.description
FROM public.con_partida_hdr h
WHERE h.company_id = @company_id
  AND h.""module"" = 'PROV'
  AND h.document_type = 'OPD'
  AND (
        btrim(coalesce(h.document_number, '')) = btrim(@document_number)
     OR btrim(coalesce(h.poliza_number, '')) = btrim(@document_number)
     OR btrim(coalesce(h.source_reference, '')) = btrim(@document_number)
  )
ORDER BY h.poliza_id DESC
LIMIT 1;";
            headerCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            headerCommand.Parameters.AddWithValue("document_number", NpgsqlDbType.Varchar, documentNumber);

            long polizaId;
            string numeroPartida;
            DateTime fechaPartida;
            string? descripcion;

            await using (var reader = await headerCommand.ExecuteReaderAsync(ct))
            {
                if (!await reader.ReadAsync(ct))
                {
                    return null;
                }

                polizaId = reader.GetInt64(0);
                numeroPartida = reader.IsDBNull(1) ? documentNumber : reader.GetString(1);
                fechaPartida = reader.GetDateTime(2);
                descripcion = reader.IsDBNull(3) ? null : reader.GetString(3);
            }

            await using var lineasCommand = connection.CreateCommand();
            lineasCommand.CommandText = @"
SELECT COALESCE(a.code, ''),
       a.name,
       CASE
           WHEN cc.cost_center_id IS NULL THEN NULL
           ELSE cc.code || ' - ' || cc.name
       END AS centro_costo,
       d.description,
       d.debit_amount,
       d.credit_amount
FROM public.con_partida_dtl d
LEFT JOIN public.con_plan_cuentas a
       ON a.company_id = d.company_id
      AND a.account_id = d.account_id
LEFT JOIN public.con_centro_costo cc
       ON cc.company_id = d.company_id
      AND cc.cost_center_id = d.cost_center_id
WHERE d.company_id = @company_id
  AND d.poliza_id = @poliza_id
ORDER BY d.line_number;";
            lineasCommand.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            lineasCommand.Parameters.AddWithValue("poliza_id", NpgsqlDbType.Bigint, polizaId);

            var lineas = new List<OrdenPagoDirectoPartidaContableLineaDto>();
            await using (var reader = await lineasCommand.ExecuteReaderAsync(ct))
            {
                while (await reader.ReadAsync(ct))
                {
                    lineas.Add(new OrdenPagoDirectoPartidaContableLineaDto
                    {
                        CuentaContable = reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                        NombreCuenta = reader.IsDBNull(1) ? null : reader.GetString(1),
                        CentroCosto = reader.IsDBNull(2) ? null : reader.GetString(2),
                        Descripcion = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Debito = reader.IsDBNull(4) ? 0m : reader.GetDecimal(4),
                        Credito = reader.IsDBNull(5) ? 0m : reader.GetDecimal(5)
                    });
                }
            }

            return new OrdenPagoDirectoPartidaContableDto
            {
                NumeroPartida = numeroPartida,
                FechaPartida = fechaPartida,
                Descripcion = descripcion,
                Lineas = lineas
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<OrdenPagoDirectoPartidaContableDto?> LoadPartidaContableFromCntPartidasAsync(
        int companyId,
        int numeroOrden,
        CancellationToken ct)
    {
        var documentNumber = $"OPD-{numeroOrden}";

        var partida = await _context.cnt_partidas_hdrs
            .AsNoTracking()
            .Where(x =>
                x.cod_empresa == companyId &&
                x.correlativo != null &&
                x.correlativo.Trim() == documentNumber)
            .OrderByDescending(x => x.cod_partida)
            .Select(x => new PartidaContableLegacyHeaderRow
            {
                CodPartida = x.cod_partida,
                NumeroPartida = x.correlativo ?? string.Empty,
                FechaPartida = x.fecha_partida,
                Descripcion = x.sinopsis
            })
            .FirstOrDefaultAsync(ct);

        if (partida is null)
        {
            return null;
        }

        var lineas = await _context.cnt_partidas_dtls
            .AsNoTracking()
            .Where(x => x.cod_empresa == companyId && x.cod_partida == partida.CodPartida)
            .OrderBy(x => x.correlativo)
            .Select(x => new OrdenPagoDirectoPartidaContableLineaDto
            {
                CuentaContable = x.cod_cuenta ?? string.Empty,
                NombreCuenta = null,
                CentroCosto = x.cod_centrocosto,
                Descripcion = x.concepto,
                Debito = x.cargos ?? 0m,
                Credito = x.creditos ?? 0m
            })
            .ToListAsync(ct);

        return new OrdenPagoDirectoPartidaContableDto
        {
            NumeroPartida = partida.NumeroPartida,
            FechaPartida = partida.FechaPartida?.ToDateTime(TimeOnly.MinValue) ?? DateTime.MinValue,
            Descripcion = partida.Descripcion,
            Lineas = lineas
        };
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken ct)
    {
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT 1
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @table_name
LIMIT 1;";
            command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);

            var result = await command.ExecuteScalarAsync(ct);
            return result is not null && result is not DBNull;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool ContainsIgnoreCase(string? source, string value)
    {
        return !string.IsNullOrWhiteSpace(source) &&
               source.Contains(value, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SameProviderCode(string? left, string? right)
    {
        return string.Equals(
            left?.Trim(),
            right?.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCuentaActiva(string? status)
    {
        return string.IsNullOrWhiteSpace(status) ||
               string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCuentaContraProcesamientoDisplay(
        string? cuentaContable,
        CuentaContraProcesamientoBancoInfo banco)
    {
        var codigoCuenta = AccountCodeFormatter.Format(cuentaContable);
        if (string.IsNullOrWhiteSpace(codigoCuenta))
        {
            codigoCuenta = "-";
        }
        return $"{codigoCuenta} / {NormalizeDisplaySegment(banco.BancoNombre)} / {NormalizeDisplaySegment(banco.NumeroCuenta)} / {banco.SaldoActual:N2}";
    }

    private static string NormalizeDisplaySegment(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "-"
            : value.Trim();
    }

    private static async Task<string> GenerateMonthlyPartidaNumberAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        long companyId,
        DateTime partidaDate,
        CancellationToken ct)
    {
        var year = partidaDate.Year;
        var month = partidaDate.Month;
        var prefix = $"{companyId}-{year:D4}-{month:D2}-";
        var periodKey = checked(year * 100 + month);
        var advisoryKey = unchecked((companyId << 32) ^ (uint)periodKey);

        await using (var lockCommand = connection.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = "SELECT pg_advisory_xact_lock(@advisory_key);";
            lockCommand.Parameters.AddWithValue("advisory_key", NpgsqlDbType.Bigint, advisoryKey);
            await lockCommand.ExecuteNonQueryAsync(ct);
        }

        var existingNumbers = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = @"
SELECT poliza_number
FROM public.con_partida_hdr
WHERE company_id = @company_id
  AND poliza_number LIKE @prefix_pattern;";
            command.Parameters.AddWithValue("company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("prefix_pattern", NpgsqlDbType.Varchar, prefix + "%");

            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (!reader.IsDBNull(0))
                {
                    existingNumbers.Add(reader.GetString(0));
                }
            }
        }

        var lastSequence = 0;
        foreach (var existingNumber in existingNumbers)
        {
            if (existingNumber.Length <= prefix.Length)
                continue;

            if (int.TryParse(existingNumber[prefix.Length..], out var currentSequence) &&
                currentSequence > lastSequence)
            {
                lastSequence = currentSequence;
            }
        }

        return $"{prefix}{lastSequence + 1:D6}";
    }

    private static string BuildDocumentNumber(int numeroOrden)
        => $"OPD-{numeroOrden}";

    private static string BuildPartidaNumber(int numeroOrden, string stage)
        => $"OPD-{numeroOrden}-{stage}";

    private string GetCurrentUser()
    {
        var user = _httpContextAccessor.HttpContext?.User?.Identity?.Name;
        return string.IsNullOrWhiteSpace(user) ? "api" : user.Trim();
    }

    private static string BuildProcessSavedMessage(string metodoPago, int totalTransaccionesBancarias)
    {
        var baseMessage =
            "El compromiso fue procesado y se genero una nueva partida contable contra la cuenta del proveedor.";

        if (!OrdenPagoDirectoMetodoPago.EsBancario(metodoPago))
        {
            return baseMessage;
        }

        if (totalTransaccionesBancarias <= 0)
        {
            return $"{baseMessage} No se registraron transacciones bancarias.";
        }

        var transaccionTexto = totalTransaccionesBancarias == 1
            ? "una transaccion bancaria vinculada"
            : $"{totalTransaccionesBancarias} transacciones bancarias vinculadas";

        return $"{baseMessage} Ademas, se registraron {transaccionTexto} y quedaron conciliadas automaticamente.";
    }

    private static string BuildCreateSavedMessage(int? correlativoProveedor)
    {
        if (!correlativoProveedor.HasValue)
        {
            return "El compromiso fue creado y la partida contable se guardo correctamente. El compromiso queda pendiente de procesar.";
        }

        return $"El compromiso fue creado y la partida contable se guardo correctamente. El compromiso queda pendiente de procesar. Correlativo del proveedor: {correlativoProveedor.Value}.";
    }

    private static string BuildSaveMessage(bool isCreate, int? correlativoProveedor)
    {
        var action = isCreate ? "creado" : "actualizado";
        if (!correlativoProveedor.HasValue)
        {
            return $"El compromiso fue {action} correctamente.";
        }

        return $"El compromiso fue {action} correctamente. Correlativo del proveedor: {correlativoProveedor.Value}.";
    }

    private static string NormalizeRequired(string? value, int maxLength, string errorMessage)
    {
        var normalized = NormalizeOptional(value, maxLength, errorMessage);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new ArgumentException(errorMessage);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El campo {fieldName} no puede superar {maxLength} caracteres.");
        }

        return normalized;
    }

    private sealed class HeaderRow
    {
        public int NumeroOrden { get; init; }
        public int? CorrelativoProveedor { get; init; }
        public DateTime? Fecha { get; init; }
        public string? Rtn { get; init; }
        public string? Concepto { get; init; }
        public decimal Monto { get; init; }
        public string? CuentaContable { get; init; }
        public string? CodigoProveedor { get; init; }
        public string? NombreProveedor { get; init; }
        public string? PagarA { get; init; }
        public bool Procesada { get; init; }
        public bool Anulada { get; init; }
    }

    private sealed class HeaderIdentityRow
    {
        public string? CodigoProveedor { get; init; }
        public int? CorrelativoProveedor { get; init; }
    }

    private sealed class DetailRow
    {
        public string? CodigoPresupuestario { get; init; }
        public string? Actividad { get; init; }
        public string? Programa { get; init; }
        public string? ObjetoGasto { get; init; }
        public string? CuentaGasto { get; init; }
        public string? Descripcion { get; init; }
        public decimal Monto { get; init; }
        public string? ConceptoDetalle { get; init; }
    }

    private sealed class PartidaContableHeaderRow
    {
        public long PolizaId { get; init; }
        public string NumeroPartida { get; init; } = string.Empty;
        public DateTime FechaPartida { get; init; }
        public string? Descripcion { get; init; }
    }

    private sealed class PartidaContableLegacyHeaderRow
    {
        public int CodPartida { get; init; }
        public string NumeroPartida { get; init; } = string.Empty;
        public DateOnly? FechaPartida { get; init; }
        public string? Descripcion { get; init; }
    }

    private sealed class CentroCostoDisplayRow
    {
        public string CodigoCosto { get; init; } = string.Empty;
        public string? ActividadNombre { get; init; }
        public string? ProgramaNombre { get; init; }
        public string? ObjetoGasto { get; init; }
        public string? CuentaContable { get; init; }
    }

    private sealed class CentroCostoSaveRow
    {
        public string CodigoCosto { get; init; } = string.Empty;
        public string? ProgramaCodigo { get; init; }
        public string? ActividadCodigo { get; init; }
        public string? ObjetoGasto { get; init; }
        public string? CuentaContable { get; init; }
    }

    private sealed class ProveedorMetadata
    {
        public string? CodigoProveedor { get; init; }
        public string? Nombre { get; init; }
        public string? Rtn { get; init; }
        public string? CuentaContable { get; init; }
    }

    private sealed class NormalizedDetailRow
    {
        public int NumeroLinea { get; init; }
        public string CodigoPresupuestario { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public string? ConceptoDetalle { get; init; }
        public decimal Monto { get; init; }
        public string? ObjetoGasto { get; init; }
        public string? CuentaContable { get; init; }
    }

    private sealed class PreparedOrder
    {
        public DateTime FechaCompromiso { get; init; }
        public string? CodigoProveedor { get; init; }
        public string? NombreProveedor { get; init; }
        public int FlagProveedor { get; init; }
        public string CuentaContable { get; init; } = string.Empty;
        public string? CodigoProyecto { get; init; }
        public string? Rtn { get; init; }
        public string Concepto { get; init; } = string.Empty;
        public string PagarA { get; init; } = string.Empty;
        public decimal MontoTotal { get; init; }
        public IReadOnlyList<PreparedDetailRow> Detalles { get; init; } = Array.Empty<PreparedDetailRow>();
    }

    private sealed class PreparedDetailRow
    {
        public string CodigoPresupuestario { get; init; } = string.Empty;
        public string Programa { get; init; } = string.Empty;
        public string Actividad { get; init; } = string.Empty;
        public string ObjetoGasto { get; init; } = string.Empty;
        public string CuentaGasto { get; init; } = string.Empty;
        public string Descripcion { get; init; } = string.Empty;
        public decimal Monto { get; init; }
        public string? ConceptoDetalle { get; init; }
    }

    private sealed class CuentaGastoSaveRow
    {
        public string Code { get; init; } = string.Empty;
        public string? Description { get; init; }
    }

    private sealed class CuentaPartidaAgrupada
    {
        public string CuentaContable { get; init; } = string.Empty;
        public decimal Monto { get; init; }
    }

    private sealed class CuentaContraProcesamientoBancoInfo
    {
        public CuentaContraProcesamientoBancoInfo(
            long bancoCuentaId,
            string? bancoNombre,
            string? numeroCuenta,
            string? tipoCuenta,
            decimal saldoActual)
        {
            BancoCuentaId = bancoCuentaId;
            BancoNombre = bancoNombre;
            NumeroCuenta = numeroCuenta;
            TipoCuenta = tipoCuenta;
            SaldoActual = saldoActual;
        }

        public long BancoCuentaId { get; }
        public string? BancoNombre { get; }
        public string? NumeroCuenta { get; }
        public string? TipoCuenta { get; }
        public decimal SaldoActual { get; }
    }

    private sealed class NormalizedContraProcessingLine
    {
        public int RowNumber { get; init; }
        public long AccountId { get; init; }
        public long? BancoCuentaId { get; init; }
        public string? TipoCuenta { get; init; }
        public string Description { get; init; } = string.Empty;
        public decimal Debit { get; init; }
    }

    private sealed class BankTransactionTypeConfig
    {
        public string TipoTransaccionId { get; init; } = string.Empty;
        public string Nombre { get; init; } = string.Empty;
    }

    private sealed class ProcessingBankAccountInfo
    {
        public long BancoCuentaId { get; init; }
        public string? TipoCuenta { get; init; }
        public string? CurrencyCode { get; init; }
        public decimal? Factor { get; init; }
    }

    private sealed class PresupuestoAfectacionLinea
    {
        public PresupuestoAfectacionLinea(string cuentaContable, decimal monto)
        {
            CuentaContable = cuentaContable;
            Monto = monto;
        }

        public string CuentaContable { get; }
        public decimal Monto { get; }
    }

    private sealed class BudgetDetailAffectRow
    {
        public SIAD.Core.Entities.pst_config_presupuesto_hdr Header { get; init; } = null!;
        public SIAD.Core.Entities.pst_config_presupuesto_dtl Detail { get; init; } = null!;
    }

    private sealed class CompromisoPresupuestoSnapshot
    {
        public DateTime FechaCompromiso { get; init; }
        public IReadOnlyList<CompromisoPresupuestoDetalleSnapshot> Detalles { get; init; } =
            Array.Empty<CompromisoPresupuestoDetalleSnapshot>();
    }

    private sealed class CompromisoPresupuestoDetalleSnapshot
    {
        public string? CuentaGasto { get; init; }
        public decimal Monto { get; init; }
    }
}
