using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Documento de descargo (entrega). Ver <see cref="IDescargoDocumentoService"/> y
/// <c>docs/plans/2026-08-04-requisiciones-descargos-fase6-plan.md</c>.
/// <para>
/// Es el ÚNICO que postea el par requisición/descargo: cada renglón genera una salida por
/// <c>SalidaDescargo</c> (documento_tipo DESCARGO, valorizada al promedio vigente, sin moverlo). La
/// concurrencia se serializa por candado del hdr de requisición (tope de la parcialidad) + correlativo
/// antes de los pares + pares en orden ascendente. La anulación es por reversa, nunca por UPDATE.
/// </para>
/// </summary>
public sealed class DescargoDocumentoService : IDescargoDocumentoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IInventarioPostingService _posting;
    private readonly IArticuloRollupService _rollup;
    private readonly IAlertasStockNotificador _alertas;

    public DescargoDocumentoService(
        SiadDbContext context, ICurrentCompanyService company,
        IInventarioPostingService posting, IArticuloRollupService rollup,
        IAlertasStockNotificador alertas)
    {
        _context = context;
        _company = company;
        _posting = posting;
        _rollup = rollup;
        _alertas = alertas;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lectura
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<DescargoDocumentoListItemDto>> GetAsync(
        DescargoDocumentoFilterDto? filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new DescargoDocumentoFilterDto();

        var query = from h in _context.alm_descargo_hdrs.AsNoTracking()
                    join b in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals b.id
                    from r in _context.alm_requisicion_hdrs.AsNoTracking()
                        .Where(r => r.id == h.requisicion_hdr_id).DefaultIfEmpty()
                    select new { h, b, ReqNumero = (int?)(r == null ? (int?)null : r.numero) };

        if (f.BodegaId is > 0) query = query.Where(x => x.h.bodega_id == f.BodegaId);
        if (f.Estado.HasValue) query = query.Where(x => x.h.estado == f.Estado.Value);
        if (f.RequisicionHdrId is > 0) query = query.Where(x => x.h.requisicion_hdr_id == f.RequisicionHdrId);
        if (f.Desde.HasValue) query = query.Where(x => x.h.fecha >= f.Desde.Value);
        if (f.Hasta.HasValue) query = query.Where(x => x.h.fecha <= f.Hasta.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            query = query.Where(x =>
                (x.h.motivo != null && EF.Functions.ILike(x.h.motivo, $"%{s}%"))
                || (x.h.recibido_por != null && EF.Functions.ILike(x.h.recibido_por, $"%{s}%"))
                || x.h.numero.ToString().Contains(s));
        }

        return await query
            .OrderByDescending(x => x.h.fecha).ThenByDescending(x => x.h.numero)
            .Select(x => new DescargoDocumentoListItemDto
            {
                Id = x.h.id,
                Numero = x.h.numero,
                Fecha = x.h.fecha,
                RequisicionHdrId = x.h.requisicion_hdr_id,
                RequisicionNumero = x.ReqNumero,
                BodegaId = x.b.id,
                BodegaNombre = x.b.nombre,
                Departamento = x.h.departamento,
                Total = x.h.total,
                Estado = x.h.estado,
                Renglones = x.h.lineas.Count,
                UsuarioCreacion = x.h.usuariocreacion
            })
            .ToListAsync(ct);
    }

    public async Task<DescargoDocumentoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var fila = await (from h in _context.alm_descargo_hdrs.AsNoTracking()
                          join b in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals b.id
                          from r in _context.alm_requisicion_hdrs.AsNoTracking()
                              .Where(r => r.id == h.requisicion_hdr_id).DefaultIfEmpty()
                          where h.id == id
                          select new { h, b, ReqNumero = (int?)(r == null ? (int?)null : r.numero) }).FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        var bodegaId = fila.h.bodega_id;
        var detalles = await (from d in _context.alm_descargos.AsNoTracking()
                              join a in _context.alm_articulos.AsNoTracking() on d.articulo_id equals a.id into aj
                              from a in aj.DefaultIfEmpty()
                              where d.descargo_hdr_id == id
                              orderby d.id
                              select new DescargoDocumentoDetalleDto
                              {
                                  Id = d.id,
                                  RequisicionLineaId = d.requisicion_id,
                                  ArticuloId = d.articulo_id ?? 0,
                                  CodigoArticulo = d.codigo_articulo,
                                  NombreArticulo = a == null ? null : a.descripcion,
                                  Cantidad = d.cantidad,
                                  PrecioUnitario = d.precio_unitario,
                                  Total = d.total
                              }).ToListAsync(ct);

        return new DescargoDocumentoDto
        {
            Id = fila.h.id,
            Numero = fila.h.numero,
            Fecha = fila.h.fecha,
            RequisicionHdrId = fila.h.requisicion_hdr_id,
            RequisicionNumero = fila.ReqNumero,
            BodegaId = bodegaId,
            BodegaNombre = fila.b.nombre,
            Departamento = fila.h.departamento,
            EntregadoPor = fila.h.entregado_por,
            RecibidoPor = fila.h.recibido_por,
            Motivo = fila.h.motivo,
            Observaciones = fila.h.observaciones,
            Total = fila.h.total,
            Estado = fila.h.estado,
            Posteado = fila.h.posteado,
            AnuladoPor = fila.h.anulado_por,
            FechaAnulacion = fila.h.fecha_anulacion,
            MotivoAnulacion = fila.h.motivo_anulacion,
            Uuid = fila.h.uuid,
            UsuarioCreacion = fila.h.usuariocreacion,
            FechaCreacion = fila.h.fechacreacion,
            Detalles = detalles
        };
    }

    public async Task<DescargoImpresionDto?> GetDatosImpresionAsync(int id, string impresoPor, CancellationToken ct = default)
    {
        var doc = await GetByIdAsync(id, ct);
        if (doc is null) return null;

        var empresa = await CargarEmpresaAsync(ct);
        return new DescargoImpresionDto
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = ClasificacionNormalizer.Usuario(impresoPor),
            Documento = doc,
            MontoEnLetras = doc.Total > 0m ? NumerosALetras.Convertir(doc.Total) : string.Empty
        };
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Entrega (crea + postea)
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record ItemEntrega(int? RequisicionLineaId, int ArticuloId, string? Codigo, decimal Cantidad, int ParId);

    public async Task<DescargoDocumentoDto> EntregarAsync(DescargoDocumentoDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        try
        {
            return await EntregarInternoAsync(dto, user, ct);
        }
        catch (DbUpdateException)
        {
            // Carrera: entre el corte de idempotencia y el INSERT, otra petición creó el descargo con
            // este uuid (índice único). Si ya existe, es idempotente: devolver el existente.
            if (dto.Uuid is Guid u && u != Guid.Empty)
            {
                var existente = await _context.alm_descargo_hdrs.AsNoTracking().FirstOrDefaultAsync(h => h.uuid == u, ct);
                if (existente is not null) return (await GetByIdAsync(existente.id, ct))!;
            }
            throw;
        }
    }

    private async Task<DescargoDocumentoDto> EntregarInternoAsync(DescargoDocumentoDto dto, string user, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        if (!dto.Uuid.HasValue || dto.Uuid.Value == Guid.Empty)
            throw new InvalidOperationException("El descargo debe traer un identificador de operación estable (uuid).");

        var ya = await _context.alm_descargo_hdrs.AsNoTracking().FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
        if (ya is not null) return (await GetByIdAsync(ya.id, ct))!;

        if (dto.Detalles is null || dto.Detalles.Count == 0)
            throw new InvalidOperationException("El descargo debe tener al menos un renglón.");
        foreach (var d in dto.Detalles)
            if (d.Cantidad <= 0m) throw new InvalidOperationException("La cantidad a entregar debe ser mayor que cero.");

        // Quién recibe y el departamento de entrega son obligatorios en las entregas nuevas.
        if (string.IsNullOrWhiteSpace(dto.RecibidoPor))
            throw new InvalidOperationException("Debe indicar quién recibe.");
        if (string.IsNullOrWhiteSpace(dto.Departamento))
            throw new InvalidOperationException("Debe indicar el departamento donde se entrega.");

        var contraRequisicion = dto.RequisicionHdrId is > 0;
        var motivo = Limpiar(dto.Motivo, 120);
        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // ── Determinar bodega, validar y armar los ítems ────────────────────────
        alm_requisicion_hdr? reqHdr = null;
        int bodegaId;
        var items = new List<ItemEntrega>();

        if (contraRequisicion)
        {
            // Candado del hdr de requisición PRIMERO: serializa entregas concurrentes de la misma
            // requisición (tope de la parcialidad) y frente a su anulación.
            reqHdr = await _context.alm_requisicion_hdrs
                .FromSqlInterpolated($@"SELECT * FROM alm_requisicion_hdr WHERE company_id = {companyId} AND id = {dto.RequisicionHdrId} FOR UPDATE")
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException("La requisición no existe en la empresa actual.");
            if (reqHdr.estado is not (EstadoRequisicionHdr.Aprobada or EstadoRequisicionHdr.DespachadaParcial))
                throw new InvalidOperationException("Solo se puede despachar una requisición aprobada.");
            bodegaId = reqHdr.bodega_id;

            var lineaIds = new HashSet<int>();
            foreach (var d in dto.Detalles)
            {
                if (d.RequisicionLineaId is not > 0)
                    throw new InvalidOperationException("Cada renglón debe indicar la línea de requisición que sirve.");
                if (!lineaIds.Add(d.RequisicionLineaId.Value))
                    throw new InvalidOperationException($"La línea de requisición {d.RequisicionLineaId} viene repetida en el descargo.");
            }

            // Renglones de la requisición, frescos bajo el candado del hdr.
            var reqLineas = await _context.alm_requisicions.AsNoTracking()
                .Where(l => l.requisicion_hdr_id == reqHdr.id).ToListAsync(ct);
            var porId = reqLineas.ToDictionary(l => l.id);

            // Validar pertenencia + tope de cada línea SERVIDA (no de todas las de la requisición).
            var servidos = new List<(alm_requisicion Rl, decimal Cantidad)>();
            foreach (var d in dto.Detalles)
            {
                if (!porId.TryGetValue(d.RequisicionLineaId!.Value, out var rl))
                    throw new InvalidOperationException($"La línea {d.RequisicionLineaId} no pertenece a esta requisición.");
                var pendiente = rl.cantidad - rl.cantidad_despachada;
                if (d.Cantidad > pendiente)
                    throw new InvalidOperationException(
                        $"No se puede entregar {d.Cantidad:0.####} del artículo {rl.codigo_articulo ?? (rl.articulo_id?.ToString() ?? "?")}: pendiente {pendiente:0.####}.");
                servidos.Add((rl, d.Cantidad));
            }

            // Resolver los pares SOLO de los artículos que este descargo entrega: una entrega parcial
            // no debe bloquearse porque OTRA línea de la requisición (no servida) carezca de ubicación.
            var servedArtIds = servidos
                .Select(s => s.Rl.articulo_id ?? throw new InvalidOperationException("La línea de requisición no tiene artículo."))
                .Distinct().ToList();
            var pares = await ResolverParesAsync(companyId, servedArtIds, bodegaId, ct);

            foreach (var (rl, cant) in servidos)
            {
                var artId = rl.articulo_id!.Value;
                items.Add(new ItemEntrega(rl.id, artId, rl.codigo_articulo, cant, pares[artId]));
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(motivo))
                throw new InvalidOperationException("Un descargo directo (sin requisición) requiere un motivo.");
            if (dto.BodegaId <= 0) throw new InvalidOperationException("Indique la bodega del descargo.");
            var bodega = await _context.alm_bodegas.AsNoTracking().FirstOrDefaultAsync(b => b.id == dto.BodegaId, ct)
                ?? throw new InvalidOperationException("La bodega no existe en la empresa actual.");
            if (!bodega.activo) throw new InvalidOperationException($"La bodega '{bodega.nombre}' está inactiva.");
            bodegaId = dto.BodegaId;

            var artIds = dto.Detalles.Select(d => d.ArticuloId).ToList();
            if (artIds.Any(x => x <= 0)) throw new InvalidOperationException("Cada renglón debe indicar el artículo.");
            var pares = await ResolverParesAsync(companyId, artIds.Distinct().ToList(), bodegaId, ct);
            var codigos = await _context.alm_articulos.AsNoTracking()
                .Where(a => artIds.Contains(a.id)).ToDictionaryAsync(a => a.id, a => a.codigo_articulo, ct);
            foreach (var d in dto.Detalles)
                items.Add(new ItemEntrega(null, d.ArticuloId, codigos.GetValueOrDefault(d.ArticuloId), d.Cantidad, pares[d.ArticuloId]));
        }

        // ── Correlativo antes de los pares; luego pre-bloqueo de pares ascendente ──
        var numero = await SiguienteNumeroAsync(companyId, ct);
        await BloquearParesAsync(companyId, items.Select(i => i.ParId), ct);

        var hdr = new alm_descargo_hdr
        {
            numero = numero,
            fecha = fecha,
            requisicion_hdr_id = reqHdr?.id,
            bodega_id = bodegaId,
            departamento = Limpiar(dto.Departamento, 3),
            entregado_por = usuario,
            recibido_por = Limpiar(dto.RecibidoPor, 120),
            motivo = motivo,
            observaciones = Limpiar(dto.Observaciones, 1000),
            total = 0m,
            estado = EstadoDescargoHdr.Registrado,
            posteado = false,
            uuid = dto.Uuid.Value,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        _context.alm_descargo_hdrs.Add(hdr);

        var lineas = items.Select(it => new alm_descargo
        {
            fecha = fecha,
            codigo_articulo = it.Codigo,
            articulo_id = it.ArticuloId,
            cantidad = it.Cantidad,
            precio_unitario = 0m,
            total = 0m,
            // El departamento vive en la cabecera (VARCHAR 3); la columna plana es VARCHAR(2) y
            // copiarlo divergiría o truncaría. Las lecturas nuevas lo toman del hdr.
            departamento = null,
            numero_requisicion = reqHdr?.numero,
            numero_documento = numero,
            bodega_id = bodegaId,
            descargo_hdr_id = hdr.id,
            requisicion_id = it.RequisicionLineaId,
            comentario = motivo,
            uuid = Guid.NewGuid(),
            origen = OrigenDocumento.Siad,
            posteado = false,
            cabecera = hdr
        }).ToList();
        _context.alm_descargos.AddRange(lineas);
        await _context.SaveChangesAsync(ct);   // las líneas necesitan su id: es el documento del asiento

        var articulosAfectados = new SortedSet<int>();
        var cruces = new List<(int articuloId, int bodegaId)>();
        decimal total = 0m;

        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var linea = lineas[i];

            var r = await _posting.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.SalidaDescargo,
                ArticuloBodegaId = it.ParId,
                Cantidad = it.Cantidad,
                Fecha = fecha,
                DocumentoId = linea.id,
                DeferirRollup = true,
                PermitirNegativo = dto.PermitirNegativo,   // el usuario confirmó el negativo en pantalla
                Observacion = $"Descargo {numero:00000}"
            }, user, ct);

            linea.precio_unitario = r.CostoPromedioResultante;
            linea.total = Redondear2(it.Cantidad * r.CostoPromedioResultante);
            linea.posteado = true;
            linea.fecha_posteo = ahora;
            total += linea.total;

            if (it.RequisicionLineaId is int reqLineaId)
                await _context.alm_requisicions.Where(l => l.id == reqLineaId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.cantidad_despachada, l => l.cantidad_despachada + it.Cantidad), ct);

            if (r.CruzoAlerta) cruces.Add((it.ArticuloId, bodegaId));
            articulosAfectados.Add(it.ArticuloId);
        }

        hdr.total = Redondear2(total);
        hdr.posteado = true;
        hdr.fecha_posteo = ahora;
        await _context.SaveChangesAsync(ct);

        // ── Derivar el estado de la requisición (4 parcial / 5 total) ──────────────
        if (reqHdr is not null)
        {
            var quedaPendiente = await _context.alm_requisicions
                .Where(l => l.requisicion_hdr_id == reqHdr.id && l.cantidad_despachada < l.cantidad)
                .AnyAsync(ct);
            reqHdr.estado = quedaPendiente ? EstadoRequisicionHdr.DespachadaParcial : EstadoRequisicionHdr.DespachadaTotal;
            reqHdr.usuariomodificacion = usuario;
            reqHdr.fechamodificacion = ahora;
            await _context.SaveChangesAsync(ct);
        }

        await RecalcularRollupAsync(articulosAfectados, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        // Post-commit y best-effort: un solo aviso por documento con los artículos que cruzaron a
        // alerta. Un fallo del correo NO afecta el descargo, que ya está confirmado.
        await AvisarCrucesAsync(cruces, $"Descargo {numero:00000}", ct);

        return (await GetByIdAsync(hdr.id, ct))!;
    }

    private async Task AvisarCrucesAsync(
        IReadOnlyCollection<(int articuloId, int bodegaId)> cruces, string documento, CancellationToken ct)
    {
        if (cruces.Count == 0) return;
        try
        {
            await _alertas.EnviarCrucesAsync(cruces, documento, ct);
        }
        catch
        {
            // Best-effort: el descargo ya está confirmado; no se propaga un fallo de notificación.
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Anulación
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        var motivoA = (motivo ?? string.Empty).Trim();
        if (motivoA.Length == 0) throw new InvalidOperationException("Debe indicar el motivo de la anulación.");
        if (motivoA.Length > 500) throw new InvalidOperationException("El motivo no puede superar los 500 caracteres.");

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var hdr = await _context.alm_descargo_hdrs
            .FromSqlInterpolated($@"SELECT * FROM alm_descargo_hdr WHERE company_id = {companyId} AND id = {id} FOR UPDATE")
            .FirstOrDefaultAsync(ct);
        if (hdr is null) return false;
        if (hdr.estado == EstadoDescargoHdr.Anulado) return true;   // idempotente

        // Candado del hdr de requisición (si viene de una) antes de tocar pares.
        alm_requisicion_hdr? reqHdr = null;
        if (hdr.requisicion_hdr_id is int reqId)
            reqHdr = await _context.alm_requisicion_hdrs
                .FromSqlInterpolated($@"SELECT * FROM alm_requisicion_hdr WHERE company_id = {companyId} AND id = {reqId} FOR UPDATE")
                .FirstOrDefaultAsync(ct);

        var lineas = await _context.alm_descargos.AsNoTracking()
            .Where(l => l.descargo_hdr_id == id).ToListAsync(ct);

        var articuloIds = lineas.Select(l => l.articulo_id ?? 0).Where(x => x > 0).Distinct().ToList();
        var pares = await ResolverParesAsync(companyId, articuloIds, hdr.bodega_id, ct);
        await BloquearParesAsync(companyId, pares.Values, ct);

        var articulosAfectados = new SortedSet<int>();
        foreach (var linea in lineas.Where(l => l.posteado && l.articulo_id.HasValue))
        {
            // El asiento de la salida se identifica por (documento_tipo=DESCARGO, documento_id=línea.id).
            var asiento = await _context.alm_kardexs.AsNoTracking()
                .FirstOrDefaultAsync(k => k.documento_tipo == TipoDocumentoInventario.Descargo && k.documento_id == linea.id, ct);
            if (asiento is not null)
            {
                await _posting.PostearAsync(new MovimientoInventarioDto
                {
                    Tipo = TipoMovimientoInventario.Reversa,
                    ArticuloBodegaId = pares[linea.articulo_id!.Value],
                    Cantidad = linea.cantidad,
                    CostoUnitario = linea.precio_unitario,
                    Fecha = DateOnly.FromDateTime(DateTime.Today),
                    KardexIdRevertido = asiento.id,
                    DeferirRollup = true,
                    Observacion = $"Anulación del descargo {hdr.numero:00000}"
                }, user, ct);
            }

            // Devolver la cantidad a la requisición que lo originó.
            if (linea.requisicion_id is int reqLineaId)
                await _context.alm_requisicions.Where(l => l.id == reqLineaId)
                    .ExecuteUpdateAsync(s => s.SetProperty(l => l.cantidad_despachada, l => l.cantidad_despachada - linea.cantidad), ct);

            articulosAfectados.Add(linea.articulo_id!.Value);
        }

        hdr.estado = EstadoDescargoHdr.Anulado;
        hdr.anulado_por = usuario;
        hdr.fecha_anulacion = ahora;
        hdr.motivo_anulacion = motivoA;
        hdr.usuariomodificacion = usuario;
        hdr.fechamodificacion = ahora;
        await _context.SaveChangesAsync(ct);

        // Recomputar el estado de la requisición tras devolver lo entregado.
        if (reqHdr is not null && reqHdr.estado is EstadoRequisicionHdr.DespachadaParcial or EstadoRequisicionHdr.DespachadaTotal)
        {
            var algoDespachado = await _context.alm_requisicions
                .Where(l => l.requisicion_hdr_id == reqHdr.id && l.cantidad_despachada > 0m).AnyAsync(ct);
            var quedaPendiente = await _context.alm_requisicions
                .Where(l => l.requisicion_hdr_id == reqHdr.id && l.cantidad_despachada < l.cantidad).AnyAsync(ct);
            reqHdr.estado = !algoDespachado ? EstadoRequisicionHdr.Aprobada
                : quedaPendiente ? EstadoRequisicionHdr.DespachadaParcial : EstadoRequisicionHdr.DespachadaTotal;
            reqHdr.usuariomodificacion = usuario;
            reqHdr.fechamodificacion = ahora;
            await _context.SaveChangesAsync(ct);
        }

        await RecalcularRollupAsync(articulosAfectados, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Apoyo
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>Resuelve el par (artículo, bodega) de cada artículo; un descargo no da de alta pares.</summary>
    private async Task<Dictionary<int, int>> ResolverParesAsync(long companyId, List<int> articuloIds, int bodegaId, CancellationToken ct)
    {
        var filas = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.bodega_id == bodegaId && articuloIds.Contains(u.articulo_id))
            .Select(u => new { u.id, u.articulo_id, u.activo }).ToListAsync(ct);
        var mapa = new Dictionary<int, int>();
        foreach (var artId in articuloIds)
        {
            var fila = filas.FirstOrDefault(f => f.articulo_id == artId)
                ?? throw new InvalidOperationException($"El artículo {artId} no tiene ubicación en la bodega seleccionada: no se puede descargar.");
            if (!fila.activo)
                throw new InvalidOperationException($"La ubicación del artículo {artId} en esa bodega está deshabilitada.");
            mapa[artId] = fila.id;
        }
        return mapa;
    }

    private async Task BloquearParesAsync(long companyId, IEnumerable<int> filaIds, CancellationToken ct)
    {
        var ids = filaIds.Distinct().OrderBy(x => x).ToArray();
        if (ids.Length == 0) return;
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            SELECT 1 FROM alm_articulo_bodega
             WHERE company_id = {companyId} AND id = ANY({ids})
             ORDER BY id
             FOR UPDATE", ct);
    }

    private async Task RecalcularRollupAsync(SortedSet<int> articulosAfectados, CancellationToken ct)
    {
        foreach (var articuloId in articulosAfectados)
            await _rollup.RecomputeAsync(articuloId, ct);
    }

    private async Task<int> SiguienteNumeroAsync(long companyId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alm_descargo_correlativo (company_id, ultimo_numero)
            VALUES ({companyId}, 0)
            ON CONFLICT (company_id) DO NOTHING", ct);

        var contador = await _context.alm_descargo_correlativos
            .FromSqlInterpolated($@"SELECT * FROM alm_descargo_correlativo WHERE company_id = {companyId} FOR UPDATE")
            .FirstAsync(ct);

        contador.ultimo_numero += 1;
        return contador.ultimo_numero;
    }

    private static string? Limpiar(string? valor, int maximo)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var limpio = valor.Trim();
        return limpio.Length > maximo ? limpio[..maximo] : limpio;
    }

    private static decimal Redondear2(decimal x) => Math.Round(x, 2, MidpointRounding.AwayFromZero);
}
