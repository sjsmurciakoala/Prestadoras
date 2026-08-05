using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Traslado entre bodegas. Ver <see cref="ITrasladoAlmacenService"/> y el diseño
/// <c>docs/plans/2026-08-04-traslado-bodegas-transito-diseno.md</c>.
/// <para>
/// Orquesta; el kardex lo escribe <see cref="IInventarioPostingService"/> (con
/// <c>TrasladoSalida</c>/<c>TrasladoEntrada</c>). Este servicio mantiene el tránsito
/// (<c>existencia_transito</c>) y toma los candados que la concurrencia exige: el <c>hdr</c>
/// primero en recibir/anular (serializa las tandas), y todas las filas
/// <c>alm_articulo_bodega</c> en orden ascendente de id antes de postear (evita el deadlock
/// cruzado). El rollup del artículo se difiere y se hace una sola vez por artículo, ordenado.
/// </para>
/// </summary>
public sealed class TrasladoAlmacenService : ITrasladoAlmacenService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IInventarioPostingService _posting;
    private readonly IArticuloRollupService _rollup;

    public TrasladoAlmacenService(
        SiadDbContext context, ICurrentCompanyService company,
        IInventarioPostingService posting, IArticuloRollupService rollup)
    {
        _context = context;
        _company = company;
        _posting = posting;
        _rollup = rollup;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lectura
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TrasladoListItemDto>> GetAsync(
        TrasladoFilterDto? filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new TrasladoFilterDto();

        var query = from h in _context.alm_movimiento_hdrs.AsNoTracking()
                    join t in _context.alm_tipo_movimientos.AsNoTracking() on h.tipo_movimiento_id equals t.id
                    join bo in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals bo.id
                    join bd in _context.alm_bodegas.AsNoTracking() on h.bodega_destino_id equals bd.id
                    where t.clase == ClaseMovimientoInventario.Traslado
                    select new { h, t, bo, bd };

        if (f.BodegaOrigenId is > 0) query = query.Where(x => x.h.bodega_id == f.BodegaOrigenId);
        if (f.BodegaDestinoId is > 0) query = query.Where(x => x.h.bodega_destino_id == f.BodegaDestinoId);
        if (f.Desde.HasValue) query = query.Where(x => x.h.fecha >= f.Desde.Value);
        if (f.Hasta.HasValue) query = query.Where(x => x.h.fecha <= f.Hasta.Value);
        if (f.Estado.HasValue) query = query.Where(x => x.h.estado == f.Estado.Value);
        if (!string.IsNullOrWhiteSpace(f.Search))
        {
            var s = f.Search.Trim();
            query = query.Where(x =>
                EF.Functions.ILike(x.h.motivo, $"%{s}%")
                || (x.h.documento_referencia != null && EF.Functions.ILike(x.h.documento_referencia, $"%{s}%"))
                || x.h.numero.ToString().Contains(s));
        }

        var rows = await query
            .OrderByDescending(x => x.h.fecha).ThenByDescending(x => x.h.numero)
            .Select(x => new
            {
                x.h.id,
                x.h.numero,
                x.h.fecha,
                OrigenId = x.bo.id,
                OrigenNombre = x.bo.nombre,
                DestinoId = x.bd.id,
                DestinoNombre = x.bd.nombre,
                x.h.requiere_recepcion,
                x.h.motivo,
                x.h.total,
                x.h.estado,
                x.h.usuariocreacion,
                Renglones = x.h.lineas.Count,
                Enviado = x.h.lineas.Sum(l => (decimal?)l.cantidad) ?? 0m,
                Recibido = x.h.lineas.Sum(l => (decimal?)l.cantidad_recibida) ?? 0m
            })
            .ToListAsync(ct);

        return rows.Select(r => new TrasladoListItemDto
        {
            Id = r.id,
            Numero = r.numero,
            Fecha = r.fecha,
            BodegaOrigenId = r.OrigenId,
            BodegaOrigenNombre = r.OrigenNombre,
            BodegaDestinoId = r.DestinoId,
            BodegaDestinoNombre = r.DestinoNombre,
            RequiereRecepcion = r.requiere_recepcion,
            Motivo = r.motivo,
            Total = r.total,
            Estado = r.estado,
            Renglones = r.Renglones,
            PorcentajeRecibido = r.Enviado > 0m ? Math.Round(r.Recibido / r.Enviado * 100m, 2, MidpointRounding.AwayFromZero) : 0m,
            UsuarioCreacion = r.usuariocreacion
        }).ToList();
    }

    public async Task<TrasladoDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var fila = await (from h in _context.alm_movimiento_hdrs.AsNoTracking()
                          join t in _context.alm_tipo_movimientos.AsNoTracking() on h.tipo_movimiento_id equals t.id
                          join bo in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals bo.id
                          join bd in _context.alm_bodegas.AsNoTracking() on h.bodega_destino_id equals bd.id
                          where h.id == id
                          select new { h, t, bo, bd }).FirstOrDefaultAsync(ct);
        if (fila is null) return null;   // no existe, o no es un traslado (bodega_destino_id NULL)

        var origenId = fila.bo.id;
        var destinoId = fila.bd.id;

        var detalles = await (from d in _context.alm_movimiento_dtls.AsNoTracking()
                              join a in _context.alm_articulos.AsNoTracking() on d.articulo_id equals a.id
                              from uo in _context.alm_articulo_bodegas.AsNoTracking()
                                  .Where(u => u.articulo_id == d.articulo_id && u.bodega_id == origenId).DefaultIfEmpty()
                              from ud in _context.alm_articulo_bodegas.AsNoTracking()
                                  .Where(u => u.articulo_id == d.articulo_id && u.bodega_id == destinoId).DefaultIfEmpty()
                              where d.movimiento_hdr_id == id
                              orderby d.id
                              select new TrasladoDetalleDto
                              {
                                  Id = d.id,
                                  ArticuloId = d.articulo_id,
                                  CodigoArticulo = d.codigo_articulo,
                                  NombreArticulo = a.descripcion,
                                  Cantidad = d.cantidad,
                                  CantidadRecibida = d.cantidad_recibida,
                                  CostoReal = d.costo_real,
                                  Total = d.total,
                                  KardexId = d.kardex_id,
                                  ExistenciaOrigen = uo == null ? null : uo.existencia,
                                  ExistenciaDestino = ud == null ? null : ud.existencia
                              }).ToListAsync(ct);

        var recepciones = await (from r in _context.alm_traslado_recepcions.AsNoTracking()
                                 where r.movimiento_hdr_id == id
                                 orderby r.id
                                 select new TrasladoRecepcionDto
                                 {
                                     Id = r.id,
                                     Fecha = r.fecha,
                                     Observaciones = r.observaciones,
                                     UsuarioCreacion = r.usuariocreacion,
                                     FechaCreacion = r.fechacreacion,
                                     Lineas = (from rd in _context.alm_traslado_recepcion_dtls.AsNoTracking()
                                               join a in _context.alm_articulos.AsNoTracking() on rd.articulo_id equals a.id
                                               where rd.recepcion_id == r.id
                                               orderby rd.id
                                               select new TrasladoRecepcionLineaDto
                                               {
                                                   Id = rd.id,
                                                   MovimientoDtlId = rd.movimiento_dtl_id,
                                                   ArticuloId = rd.articulo_id,
                                                   NombreArticulo = a.descripcion,
                                                   Cantidad = rd.cantidad,
                                                   CostoReal = rd.costo_real,
                                                   Total = rd.total,
                                                   KardexId = rd.kardex_id
                                               }).ToList()
                                 }).ToListAsync(ct);

        return new TrasladoDto
        {
            Id = fila.h.id,
            Numero = fila.h.numero,
            TipoMovimientoId = fila.t.id,
            TipoCodigo = fila.t.codigo,
            TipoNombre = fila.t.nombre,
            BodegaOrigenId = origenId,
            BodegaOrigenNombre = fila.bo.nombre,
            BodegaDestinoId = destinoId,
            BodegaDestinoNombre = fila.bd.nombre,
            RequiereRecepcion = fila.h.requiere_recepcion,
            Fecha = fila.h.fecha,
            Motivo = fila.h.motivo,
            DocumentoReferencia = fila.h.documento_referencia,
            Observaciones = fila.h.observaciones,
            Total = fila.h.total,
            Estado = fila.h.estado,
            RecibidoPor = fila.h.recibido_por,
            FechaRecepcion = fila.h.fecha_recepcion,
            AnuladoPor = fila.h.anulado_por,
            FechaAnulacion = fila.h.fecha_anulacion,
            MotivoAnulacion = fila.h.motivo_anulacion,
            Uuid = fila.h.uuid,
            UsuarioCreacion = fila.h.usuariocreacion,
            FechaCreacion = fila.h.fechacreacion,
            Detalles = detalles,
            Recepciones = recepciones
        };
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Envío (con recepción o directo)
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<TrasladoDto> EnviarAsync(TrasladoDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        // Contrato de idempotencia: la UI manda un uuid estable por operación (no lo generamos aquí,
        // para que un reintento por respuesta perdida no cree un traslado duplicado).
        if (!dto.Uuid.HasValue || dto.Uuid.Value == Guid.Empty)
            throw new InvalidOperationException("El traslado debe traer un identificador de operación estable (uuid).");

        var yaHdr = await _context.alm_movimiento_hdrs.AsNoTracking()
            .FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
        if (yaHdr is not null) return (await GetByIdAsync(yaHdr.id, ct))!;

        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) throw new InvalidOperationException("El motivo del traslado es obligatorio.");
        if (motivo.Length > 120) throw new InvalidOperationException("El motivo no puede superar los 120 caracteres.");

        if (dto.BodegaOrigenId <= 0 || dto.BodegaDestinoId <= 0)
            throw new InvalidOperationException("Debe indicar la bodega de origen y la de destino.");
        if (dto.BodegaOrigenId == dto.BodegaDestinoId)
            throw new InvalidOperationException("El origen y el destino no pueden ser la misma bodega.");
        if (dto.Detalles is null || dto.Detalles.Count == 0)
            throw new InvalidOperationException("El traslado debe tener al menos un renglón.");

        var tipo = await _context.alm_tipo_movimientos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.id == dto.TipoMovimientoId, ct)
            ?? throw new InvalidOperationException("El tipo de movimiento no existe en la empresa actual.");
        if (!tipo.activo) throw new InvalidOperationException($"El tipo de movimiento '{tipo.nombre}' está inactivo.");
        if (tipo.clase != ClaseMovimientoInventario.Traslado)
            throw new InvalidOperationException($"El tipo '{tipo.nombre}' no es de clase TRASLADO.");

        var origen = await _context.alm_bodegas.AsNoTracking().FirstOrDefaultAsync(b => b.id == dto.BodegaOrigenId, ct)
            ?? throw new InvalidOperationException("La bodega de origen no existe en la empresa actual.");
        if (!origen.activo) throw new InvalidOperationException($"La bodega de origen '{origen.nombre}' está inactiva.");
        var destino = await _context.alm_bodegas.AsNoTracking().FirstOrDefaultAsync(b => b.id == dto.BodegaDestinoId, ct)
            ?? throw new InvalidOperationException("La bodega de destino no existe en la empresa actual.");
        if (!destino.activo) throw new InvalidOperationException($"La bodega de destino '{destino.nombre}' está inactiva.");

        ValidarRenglonesEnvio(dto.Detalles);

        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var articuloIds = dto.Detalles.Select(d => d.ArticuloId).Distinct().ToList();

        // El correlativo por empresa se toma PRIMERO, antes de resolver o bloquear pares, para que
        // este flujo adquiera los candados en el MISMO orden que MovimientoAlmacenService (correlativo
        // → pares). Si el traslado tomara los pares primero y el correlativo después, un movimiento y
        // un traslado concurrentes sobre el mismo par de la misma empresa harían un deadlock ABBA
        // (uno abortado con 40P01). Hallazgo de la revisión adversarial de la Fase 5.3.
        var numero = await SiguienteNumeroAsync(companyId, ct);

        var paresOrigen = await ResolverParesActivosAsync(companyId, articuloIds, dto.BodegaOrigenId, crearSiFalta: false, ct);
        var paresDestino = await ResolverParesActivosAsync(companyId, articuloIds, dto.BodegaDestinoId, crearSiFalta: true, ct);

        // Pre-bloqueo de TODAS las filas afectadas (origen + destino) en orden ascendente de id.
        await BloquearParesAsync(companyId,
            paresOrigen.Values.Select(p => p.ParId).Concat(paresDestino.Values.Select(p => p.ParId)), ct);

        var hdrUuid = dto.Uuid.Value;

        var cabecera = new alm_movimiento_hdr
        {
            numero = numero,
            tipo_movimiento_id = tipo.id,
            bodega_id = dto.BodegaOrigenId,
            bodega_destino_id = dto.BodegaDestinoId,
            requiere_recepcion = dto.RequiereRecepcion,
            fecha = fecha,
            motivo = motivo,
            documento_referencia = Limpiar(dto.DocumentoReferencia, 30),
            observaciones = Limpiar(dto.Observaciones, 1000),
            total = 0m,
            estado = EstadoMovimientoAlmacen.EnTransito,
            posteado = false,
            uuid = hdrUuid,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        var lineas = dto.Detalles
            .OrderBy(d => paresOrigen[d.ArticuloId].ParId)
            .Select((d, i) => new alm_movimiento_dtl
            {
                cabecera = cabecera,
                articulo_id = d.ArticuloId,
                codigo_articulo = paresOrigen[d.ArticuloId].Codigo,
                cantidad = d.Cantidad,
                cantidad_recibida = 0m,
                costo_unitario = 0m,   // el traslado sale al promedio de origen, no se teclea costo
                uuid = UuidV5.CreateInventario($"traslado|{companyId}|{hdrUuid}|{i}"),
                posteado = false
            })
            .ToList();

        _context.alm_movimiento_hdrs.Add(cabecera);
        _context.alm_movimiento_dtls.AddRange(lineas);
        await _context.SaveChangesAsync(ct);   // las líneas necesitan su id (documento del asiento de salida)

        var articulosAfectados = new SortedSet<int>();
        decimal total = 0m;

        foreach (var linea in lineas)
        {
            var parO = paresOrigen[linea.articulo_id];
            var parD = paresDestino[linea.articulo_id];

            var r = await _posting.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.TrasladoSalida,
                ArticuloBodegaId = parO.ParId,
                Cantidad = linea.cantidad,
                Fecha = fecha,
                DocumentoId = linea.id,
                BodegaDestinoId = dto.BodegaDestinoId,
                DeferirRollup = true,
                Observacion = $"{tipo.codigo} · {motivo}"
            }, user, ct);

            // La salida sale al promedio vigente de origen: ese es el costo que viaja con la mercadería.
            linea.kardex_id = r.KardexId;
            linea.costo_real = r.CostoPromedioResultante;
            linea.total = Redondear2(linea.cantidad * (linea.costo_real ?? 0m));
            linea.posteado = true;
            total += linea.total;

            // Carga el tránsito de destino (SQL atómico, bajo el candado ya tomado).
            await _context.alm_articulo_bodegas.Where(u => u.id == parD.ParId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    u => u.existencia_transito, u => u.existencia_transito + linea.cantidad), ct);

            articulosAfectados.Add(linea.articulo_id);
        }

        cabecera.total = Redondear2(total);
        cabecera.posteado = true;
        cabecera.fecha_posteo = ahora;
        await _context.SaveChangesAsync(ct);

        // Directo (un paso): recepción total automática en la misma transacción.
        if (!dto.RequiereRecepcion)
        {
            var items = lineas.Select(l => new ItemRecepcion(
                l.id, l.articulo_id, paresDestino[l.articulo_id].ParId, l.cantidad, l.costo_real ?? 0m)).ToList();

            var actoUuid = UuidV5.CreateInventario($"traslado_recepcion_auto|{companyId}|{hdrUuid}");
            await PostearRecepcionNucleoAsync(
                companyId, cabecera, items, actoUuid, fecha,
                "Recepción automática (traslado directo)", user, ahora, articulosAfectados, ct);
        }

        await RecalcularRollupAsync(articulosAfectados, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return (await GetByIdAsync(cabecera.id, ct))!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Recepción parcial
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<TrasladoDto> RecibirAsync(
        int trasladoId, RecepcionTrasladoDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        if (!dto.Uuid.HasValue || dto.Uuid.Value == Guid.Empty)
            throw new InvalidOperationException("La recepción debe traer un identificador de operación estable (uuid).");
        if (dto.Lineas is null || dto.Lineas.Count == 0)
            throw new InvalidOperationException("Indique al menos un renglón a recibir.");

        // Idempotencia del ACTO de recepción, antes de tocar nada.
        var yaRec = await _context.alm_traslado_recepcions.AsNoTracking()
            .FirstOrDefaultAsync(r => r.uuid == dto.Uuid.Value, ct);
        if (yaRec is not null) return (await GetByIdAsync(yaRec.movimiento_hdr_id, ct))!;

        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // Candado del hdr PRIMERO: serializa todas las recepciones y anulaciones del mismo traslado.
        var cabecera = await BloquearHdrAsync(companyId, trasladoId, ct);
        if (cabecera is null) throw new InvalidOperationException("El traslado no existe en la empresa actual.");
        if (cabecera.bodega_destino_id is null)
            throw new InvalidOperationException("El documento no es un traslado.");
        if (cabecera.estado != EstadoMovimientoAlmacen.EnTransito)
            throw new InvalidOperationException("Solo se puede recibir un traslado que esté en tránsito.");

        // Renglones frescos BAJO el candado del hdr.
        var renglones = await _context.alm_movimiento_dtls.AsNoTracking()
            .Where(l => l.movimiento_hdr_id == trasladoId).ToListAsync(ct);
        var porId = renglones.ToDictionary(l => l.id);

        // Validar: sin renglones repetidos, pertenencia y no sobre-recepción.
        var vistos = new HashSet<int>();
        foreach (var ln in dto.Lineas)
        {
            if (!vistos.Add(ln.MovimientoDtlId))
                throw new InvalidOperationException($"El renglón {ln.MovimientoDtlId} viene repetido en la recepción.");
            if (!porId.TryGetValue(ln.MovimientoDtlId, out var renglon))
                throw new InvalidOperationException($"El renglón {ln.MovimientoDtlId} no pertenece a este traslado.");
            if (ln.Cantidad <= 0m)
                throw new InvalidOperationException("La cantidad a recibir debe ser mayor que cero.");
            var pendiente = renglon.cantidad - renglon.cantidad_recibida;
            if (ln.Cantidad > pendiente)
                throw new InvalidOperationException(
                    $"No se puede recibir {ln.Cantidad:0.####} del artículo " +
                    $"{renglon.codigo_articulo ?? renglon.articulo_id.ToString()}: pendiente {pendiente:0.####}.");
        }

        var destinoId = cabecera.bodega_destino_id.Value;
        var articuloIds = dto.Lineas.Select(l => porId[l.MovimientoDtlId].articulo_id).Distinct().ToList();
        var paresDestino = await ResolverParesActivosAsync(companyId, articuloIds, destinoId, crearSiFalta: false, ct);

        var items = dto.Lineas.Select(ln =>
        {
            var renglon = porId[ln.MovimientoDtlId];
            return new ItemRecepcion(renglon.id, renglon.articulo_id,
                paresDestino[renglon.articulo_id].ParId, ln.Cantidad, renglon.costo_real ?? 0m);
        }).ToList();

        // Pre-bloqueo de las filas de destino en orden ascendente de id.
        await BloquearParesAsync(companyId, items.Select(i => i.DestinoParId), ct);

        var articulosAfectados = new SortedSet<int>();
        await PostearRecepcionNucleoAsync(
            companyId, cabecera, items, dto.Uuid.Value, fecha,
            Limpiar(dto.Observaciones, 500), user, ahora, articulosAfectados, ct);

        await RecalcularRollupAsync(articulosAfectados, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return (await GetByIdAsync(trasladoId, ct))!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Anulación
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        var motivoAnulacion = (motivo ?? string.Empty).Trim();
        if (motivoAnulacion.Length == 0) throw new InvalidOperationException("Debe indicar el motivo de la anulación.");
        if (motivoAnulacion.Length > 500) throw new InvalidOperationException("El motivo de anulación no puede superar los 500 caracteres.");

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var cabecera = await BloquearHdrAsync(companyId, id, ct);
        if (cabecera is null) return false;
        if (cabecera.bodega_destino_id is null)
            throw new InvalidOperationException("El documento no es un traslado; anúlelo desde movimientos.");
        if (cabecera.estado == EstadoMovimientoAlmacen.Anulado) return true;   // idempotente

        var destinoId = cabecera.bodega_destino_id.Value;

        // Renglones (salidas de origen) y entradas ya posteadas (recepciones), frescos bajo el candado.
        var renglones = await _context.alm_movimiento_dtls.AsNoTracking()
            .Where(l => l.movimiento_hdr_id == id).ToListAsync(ct);
        var entradas = await (from rd in _context.alm_traslado_recepcion_dtls.AsNoTracking()
                              join r in _context.alm_traslado_recepcions.AsNoTracking() on rd.recepcion_id equals r.id
                              where r.movimiento_hdr_id == id && rd.kardex_id != null
                              select rd).ToListAsync(ct);

        var articuloIds = renglones.Select(l => l.articulo_id).Distinct().ToList();
        var paresOrigen = await MapearParesAsync(companyId, articuloIds, cabecera.bodega_id, ct);
        var paresDestino = await MapearParesAsync(companyId, articuloIds, destinoId, ct);

        // Pre-bloqueo de todas las filas (origen + destino) en orden ascendente antes de revertir nada.
        await BloquearParesAsync(companyId,
            paresOrigen.Values.Concat(paresDestino.Values), ct);

        var articulosAfectados = new SortedSet<int>();

        // 1) Revertir las entradas ya recibidas en destino (guarda de negativo del motor).
        foreach (var rd in entradas)
        {
            await _posting.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.Reversa,
                ArticuloBodegaId = paresDestino[rd.articulo_id],
                Cantidad = rd.cantidad,
                CostoUnitario = rd.costo_real,
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                KardexIdRevertido = rd.kardex_id!.Value,
                DeferirRollup = true,
                Observacion = $"Anulación del traslado {cabecera.numero:00000}"
            }, user, ct);
            articulosAfectados.Add(rd.articulo_id);
        }

        // 2) Descargar el tránsito pendiente de destino y revertir la salida de origen (devolución).
        foreach (var linea in renglones)
        {
            var pendiente = linea.cantidad - linea.cantidad_recibida;
            if (pendiente > 0m)
            {
                await _context.alm_articulo_bodegas.Where(u => u.id == paresDestino[linea.articulo_id])
                    .ExecuteUpdateAsync(s => s.SetProperty(
                        u => u.existencia_transito, u => u.existencia_transito - pendiente), ct);
            }

            if (linea.posteado && linea.kardex_id.HasValue)
            {
                await _posting.PostearAsync(new MovimientoInventarioDto
                {
                    Tipo = TipoMovimientoInventario.Reversa,
                    ArticuloBodegaId = paresOrigen[linea.articulo_id],
                    Cantidad = linea.cantidad,
                    CostoUnitario = linea.costo_real ?? 0m,
                    Fecha = DateOnly.FromDateTime(DateTime.Today),
                    KardexIdRevertido = linea.kardex_id.Value,
                    DeferirRollup = true,
                    Observacion = $"Anulación del traslado {cabecera.numero:00000}"
                }, user, ct);
            }
            articulosAfectados.Add(linea.articulo_id);
        }

        cabecera.estado = EstadoMovimientoAlmacen.Anulado;
        cabecera.anulado_por = usuario;
        cabecera.fecha_anulacion = ahora;
        cabecera.motivo_anulacion = motivoAnulacion;
        cabecera.usuariomodificacion = usuario;
        cabecera.fechamodificacion = ahora;
        await _context.SaveChangesAsync(ct);

        await RecalcularRollupAsync(articulosAfectados, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Núcleo de recepción (compartido por RecibirAsync y el modo directo)
    // ─────────────────────────────────────────────────────────────────────────

    private sealed record ItemRecepcion(int RenglonId, int ArticuloId, int DestinoParId, decimal Cantidad, decimal CostoReal);

    /// <summary>
    /// Postea una tanda de recepción: crea el acto y sus líneas, y por cada una libera el tránsito,
    /// postea la entrada a destino y sube <c>cantidad_recibida</c> con delta en SQL. Si el traslado
    /// queda completo, lo deja Recibido. Asume que el hdr ya está bloqueado y las filas de destino
    /// pre-bloqueadas por el llamador.
    /// </summary>
    private async Task PostearRecepcionNucleoAsync(
        long companyId, alm_movimiento_hdr cabecera, IReadOnlyList<ItemRecepcion> items,
        Guid actoUuid, DateOnly fecha, string? observaciones, string user, DateTime ahora,
        SortedSet<int> articulosAfectados, CancellationToken ct)
    {
        var usuario = ClasificacionNormalizer.Usuario(user);

        var recepcion = new alm_traslado_recepcion
        {
            movimiento_hdr_id = cabecera.id,
            fecha = fecha,
            observaciones = observaciones,
            uuid = actoUuid,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        _context.alm_traslado_recepcions.Add(recepcion);
        await _context.SaveChangesAsync(ct);   // necesita id para las líneas

        var lineasRec = items.Select((it, i) => new alm_traslado_recepcion_dtl
        {
            recepcion = recepcion,
            movimiento_dtl_id = it.RenglonId,
            articulo_id = it.ArticuloId,
            cantidad = it.Cantidad,
            costo_real = it.CostoReal,
            total = Redondear2(it.Cantidad * it.CostoReal),
            uuid = UuidV5.CreateInventario($"traslado_rec|{companyId}|{actoUuid}|{it.RenglonId}")
        }).ToList();
        _context.alm_traslado_recepcion_dtls.AddRange(lineasRec);
        await _context.SaveChangesAsync(ct);   // necesita id: es el documento del asiento de entrada

        for (var i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var lineaRec = lineasRec[i];

            // Libera el tránsito de destino (el CHECK >= 0 es el backstop).
            await _context.alm_articulo_bodegas.Where(u => u.id == it.DestinoParId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    u => u.existencia_transito, u => u.existencia_transito - it.Cantidad), ct);

            var r = await _posting.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.TrasladoEntrada,
                ArticuloBodegaId = it.DestinoParId,
                Cantidad = it.Cantidad,
                CostoUnitario = it.CostoReal,
                Fecha = fecha,
                DocumentoId = lineaRec.id,
                BodegaDestinoId = cabecera.bodega_id,   // informativa: la contraparte (origen)
                DeferirRollup = true,
                Observacion = $"Recepción del traslado {cabecera.numero:00000}"
            }, user, ct);

            lineaRec.kardex_id = r.KardexId;

            // Sube cantidad_recibida con delta en SQL: así el CHECK (<= cantidad) es un backstop real
            // y no un valor absoluto calculado en memoria (evita el lost update).
            await _context.alm_movimiento_dtls.Where(l => l.id == it.RenglonId)
                .ExecuteUpdateAsync(s => s.SetProperty(
                    l => l.cantidad_recibida, l => l.cantidad_recibida + it.Cantidad), ct);

            articulosAfectados.Add(it.ArticuloId);
        }

        await _context.SaveChangesAsync(ct);   // persiste kardex_id de las líneas de recepción

        // ¿Quedó algo por recibir? Se evalúa en el servidor (AnyAsync), sobre valores frescos de la BD,
        // bajo el candado del hdr — no sobre entidades trackeadas que podrían estar desactualizadas.
        var quedaPendiente = await _context.alm_movimiento_dtls
            .Where(l => l.movimiento_hdr_id == cabecera.id && l.cantidad_recibida < l.cantidad)
            .AnyAsync(ct);

        if (!quedaPendiente)
        {
            cabecera.estado = EstadoMovimientoAlmacen.Recibido;
            cabecera.recibido_por = usuario;
            cabecera.fecha_recepcion = ahora;
            cabecera.usuariomodificacion = usuario;
            cabecera.fechamodificacion = ahora;
            await _context.SaveChangesAsync(ct);
        }
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Apoyo
    // ─────────────────────────────────────────────────────────────────────────

    private static void ValidarRenglonesEnvio(List<TrasladoDetalleDto> detalles)
    {
        for (var i = 0; i < detalles.Count; i++)
        {
            var d = detalles[i];
            var n = i + 1;
            if (d.ArticuloId <= 0) throw new InvalidOperationException($"El renglón {n} no tiene artículo.");
            if (d.Cantidad <= 0m) throw new InvalidOperationException($"El renglón {n}: la cantidad debe ser mayor que cero.");
        }
        var repetido = detalles.GroupBy(d => d.ArticuloId).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw new InvalidOperationException(
                $"El artículo {repetido.First().CodigoArticulo ?? repetido.Key.ToString()} está en más de un renglón. Únalos en uno solo.");
    }

    private sealed record ParInfo(int ParId, string? Codigo);

    /// <summary>
    /// Resuelve el par (artículo, bodega) de cada artículo, rechazando pares inactivos (que el rollup
    /// no sumaría y descuadrarían el total). Si <paramref name="crearSiFalta"/>, crea el par que no
    /// exista con <c>INSERT … ON CONFLICT DO NOTHING</c> (dos envíos concurrentes convergen a la misma
    /// fila sin excepción cruda) y lo relee.
    /// </summary>
    private async Task<Dictionary<int, ParInfo>> ResolverParesActivosAsync(
        long companyId, List<int> articuloIds, int bodegaId, bool crearSiFalta, CancellationToken ct)
    {
        var filas = await (from u in _context.alm_articulo_bodegas.AsNoTracking()
                           join a in _context.alm_articulos.AsNoTracking() on u.articulo_id equals a.id
                           where u.bodega_id == bodegaId && articuloIds.Contains(u.articulo_id)
                           select new { u.id, u.articulo_id, u.activo, a.codigo_articulo }).ToListAsync(ct);

        var mapa = new Dictionary<int, ParInfo>();
        foreach (var articuloId in articuloIds)
        {
            var fila = filas.FirstOrDefault(f => f.articulo_id == articuloId);
            if (fila is null)
            {
                if (!crearSiFalta)
                    throw new InvalidOperationException(
                        $"El artículo {articuloId} no tiene ubicación en la bodega seleccionada. Créela antes de trasladarlo.");

                await _context.Database.ExecuteSqlInterpolatedAsync($@"
                    INSERT INTO alm_articulo_bodega
                        (company_id, articulo_id, bodega_id, existencia, existencia_minima, existencia_maxima,
                         punto_reorden, existencia_comprometida, existencia_transito, costo_promedio, ultimo_costo,
                         principal, activo, usuariocreacion, fechacreacion)
                    VALUES ({companyId}, {articuloId}, {bodegaId}, 0, 0, 0, 0, 0, 0, 0, 0,
                            false, true, 'traslado', (now() AT TIME ZONE 'utc'))
                    ON CONFLICT (company_id, articulo_id, bodega_id) DO NOTHING", ct);

                var nueva = await _context.alm_articulo_bodegas.AsNoTracking()
                    .Where(u => u.articulo_id == articuloId && u.bodega_id == bodegaId)
                    .Select(u => new { u.id, u.activo })
                    .FirstAsync(ct);
                if (!nueva.activo)
                    throw new InvalidOperationException(
                        $"La ubicación del artículo {articuloId} en la bodega destino está deshabilitada: reactívela antes de trasladar.");
                mapa[articuloId] = new ParInfo(nueva.id, null);
                continue;
            }
            if (!fila.activo)
                throw new InvalidOperationException(
                    $"La ubicación del artículo {fila.codigo_articulo} en esa bodega está deshabilitada: reactívela antes de trasladar.");
            mapa[articuloId] = new ParInfo(fila.id, fila.codigo_articulo);
        }
        return mapa;
    }

    /// <summary>Mapea artículo → par id sin validar activo ni crear (para anular: hay que revertir igual).</summary>
    private async Task<Dictionary<int, int>> MapearParesAsync(
        long companyId, List<int> articuloIds, int bodegaId, CancellationToken ct)
    {
        var filas = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.bodega_id == bodegaId && articuloIds.Contains(u.articulo_id))
            .Select(u => new { u.id, u.articulo_id }).ToListAsync(ct);
        var mapa = new Dictionary<int, int>();
        foreach (var f in filas) mapa[f.articulo_id] = f.id;
        foreach (var articuloId in articuloIds)
            if (!mapa.ContainsKey(articuloId))
                throw new InvalidOperationException($"No se encontró la ubicación del artículo {articuloId} en la bodega {bodegaId}.");
        return mapa;
    }

    /// <summary>
    /// Bloquea las filas <c>alm_articulo_bodega</c> indicadas con <c>FOR UPDATE</c> en orden ASCENDENTE
    /// de id. El orden estable evita el deadlock cruzado entre dos traslados que toquen los mismos pares
    /// en direcciones opuestas. El motor re-pide el candado de cada fila (no-op: ya tomado en esta tx).
    /// </summary>
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

    private async Task<alm_movimiento_hdr?> BloquearHdrAsync(long companyId, int id, CancellationToken ct)
        => await _context.alm_movimiento_hdrs
            .FromSqlInterpolated($@"
                SELECT * FROM alm_movimiento_hdr
                 WHERE company_id = {companyId} AND id = {id}
                 FOR UPDATE")
            .FirstOrDefaultAsync(ct);

    private async Task RecalcularRollupAsync(SortedSet<int> articulosAfectados, CancellationToken ct)
    {
        // Una sola vez por artículo, en orden ascendente de id: cierra el deadlock ABBA sobre
        // alm_articulo entre traslados multi-bodega y evita rollups redundantes.
        foreach (var articuloId in articulosAfectados)
            await _rollup.RecomputeAsync(articuloId, ct);
    }

    private async Task<int> SiguienteNumeroAsync(long companyId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alm_movimiento_correlativo (company_id, ultimo_numero)
            VALUES ({companyId}, 0)
            ON CONFLICT (company_id) DO NOTHING", ct);

        var contador = await _context.alm_movimiento_correlativos
            .FromSqlInterpolated($@"
                SELECT * FROM alm_movimiento_correlativo
                 WHERE company_id = {companyId}
                 FOR UPDATE")
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
