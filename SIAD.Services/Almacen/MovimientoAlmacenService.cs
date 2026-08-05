using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Documento de movimiento de almacén. Ver <see cref="IMovimientoAlmacenService"/>.
/// <para>
/// Este servicio orquesta; no calcula inventario. Traduce la <c>clase</c> del tipo a un
/// <see cref="TipoMovimientoInventario"/> y deja que <see cref="IInventarioPostingService"/>
/// —único punto de escritura del kardex, con su <c>FOR UPDATE</c> y su idempotencia— haga
/// el trabajo.
/// </para>
/// </summary>
public sealed class MovimientoAlmacenService : IMovimientoAlmacenService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IInventarioPostingService _posting;

    public MovimientoAlmacenService(
        SiadDbContext context, ICurrentCompanyService company, IInventarioPostingService posting)
    {
        _context = context;
        _company = company;
        _posting = posting;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Lectura
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<MovimientoAlmacenListItemDto>> GetAsync(
        MovimientoAlmacenFilterDto? filtro, CancellationToken ct = default)
    {
        var f = filtro ?? new MovimientoAlmacenFilterDto();

        var query = from h in _context.alm_movimiento_hdrs.AsNoTracking()
                    join t in _context.alm_tipo_movimientos.AsNoTracking() on h.tipo_movimiento_id equals t.id
                    join b in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals b.id
                    select new { h, t, b };

        if (f.TipoMovimientoId is > 0) query = query.Where(x => x.h.tipo_movimiento_id == f.TipoMovimientoId);
        if (f.BodegaId is > 0) query = query.Where(x => x.h.bodega_id == f.BodegaId);
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

        return await query
            .OrderByDescending(x => x.h.fecha).ThenByDescending(x => x.h.numero)
            .Select(x => new MovimientoAlmacenListItemDto
            {
                Id = x.h.id,
                Numero = x.h.numero,
                Fecha = x.h.fecha,
                TipoMovimientoId = x.t.id,
                TipoCodigo = x.t.codigo,
                TipoNombre = x.t.nombre,
                Clase = x.t.clase,
                BodegaId = x.b.id,
                BodegaNombre = x.b.nombre,
                Motivo = x.h.motivo,
                DocumentoReferencia = x.h.documento_referencia,
                Total = x.h.total,
                Estado = x.h.estado,
                Renglones = x.h.lineas.Count,
                UsuarioCreacion = x.h.usuariocreacion
            })
            .ToListAsync(ct);
    }

    public async Task<MovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        var fila = await (from h in _context.alm_movimiento_hdrs.AsNoTracking()
                          join t in _context.alm_tipo_movimientos.AsNoTracking() on h.tipo_movimiento_id equals t.id
                          join b in _context.alm_bodegas.AsNoTracking() on h.bodega_id equals b.id
                          where h.id == id
                          select new { h, t, b }).FirstOrDefaultAsync(ct);
        if (fila is null) return null;

        // Existencia actual del par, para que la pantalla la muestre junto al renglón.
        var detalles = await (from d in _context.alm_movimiento_dtls.AsNoTracking()
                              join a in _context.alm_articulos.AsNoTracking() on d.articulo_id equals a.id
                              from u in _context.alm_articulo_bodegas.AsNoTracking()
                                  .Where(u => u.articulo_id == d.articulo_id && u.bodega_id == fila.h.bodega_id)
                                  .DefaultIfEmpty()
                              where d.movimiento_hdr_id == id
                              orderby d.id
                              select new MovimientoAlmacenDetalleDto
                              {
                                  Id = d.id,
                                  ArticuloId = d.articulo_id,
                                  CodigoArticulo = d.codigo_articulo,
                                  NombreArticulo = a.descripcion,
                                  Cantidad = d.cantidad,
                                  CostoUnitario = d.costo_unitario,
                                  CostoReal = d.costo_real,
                                  Total = d.total,
                                  KardexId = d.kardex_id,
                                  ExistenciaActual = u == null ? null : u.existencia
                              }).ToListAsync(ct);

        return new MovimientoAlmacenDto
        {
            Id = fila.h.id,
            Numero = fila.h.numero,
            TipoMovimientoId = fila.t.id,
            TipoCodigo = fila.t.codigo,
            TipoNombre = fila.t.nombre,
            Clase = fila.t.clase,
            BodegaId = fila.b.id,
            BodegaNombre = fila.b.nombre,
            Fecha = fila.h.fecha,
            Motivo = fila.h.motivo,
            DocumentoReferencia = fila.h.documento_referencia,
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

    public Task<bool> TipoTieneMovimientosPosteadosAsync(int tipoMovimientoId, CancellationToken ct = default)
        => _context.alm_movimiento_dtls.AsNoTracking()
            .AnyAsync(d => d.posteado && d.cabecera.tipo_movimiento_id == tipoMovimientoId, ct);

    // ─────────────────────────────────────────────────────────────────────────
    // Captura y posteo
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<MovimientoAlmacenDto> CrearYPostearAsync(
        MovimientoAlmacenDto dto, string user, bool puedeUsarTiposSensibles, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        // ── Idempotencia del DOCUMENTO, antes de tocar nada ──────────────────
        if (dto.Uuid.HasValue)
        {
            var ya = await _context.alm_movimiento_hdrs.AsNoTracking()
                .FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
            if (ya is not null) return (await GetByIdAsync(ya.id, ct))!;
        }

        var motivo = (dto.Motivo ?? string.Empty).Trim();
        if (motivo.Length == 0) throw new InvalidOperationException("El motivo del movimiento es obligatorio.");
        if (motivo.Length > 120) throw new InvalidOperationException("El motivo no puede superar los 120 caracteres.");

        if (dto.Detalles is null || dto.Detalles.Count == 0)
            throw new InvalidOperationException("El movimiento debe tener al menos un renglón.");

        // El filtro global de empresa hace que un tipo de otro tenant simplemente no exista.
        var tipo = await _context.alm_tipo_movimientos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.id == dto.TipoMovimientoId, ct)
            ?? throw new InvalidOperationException("El tipo de movimiento no existe en la empresa actual.");

        if (!tipo.activo)
            throw new InvalidOperationException(
                $"El tipo de movimiento '{tipo.nombre}' está inactivo y no se puede usar en documentos nuevos.");

        // El traslado entre bodegas es un documento de dos bodegas y (opcionalmente) dos pasos:
        // se captura en su propia pantalla/servicio (TrasladoAlmacenService), no aquí. Rechazar
        // explícito da un mensaje accionable en vez del throw genérico de TipoMovimientoDe.
        if (tipo.clase == ClaseMovimientoInventario.Traslado)
            throw new InvalidOperationException(
                "Los traslados entre bodegas se registran desde la pantalla de Traslados, no en movimientos de entrada/salida.");

        // Tipos sensibles: la bandera del catálogo exige un permiso adicional. Se comprueba
        // aquí y no sólo en el controller para que valga por cualquier vía de entrada.
        if (tipo.requiere_autorizacion && !puedeUsarTiposSensibles)
        {
            throw new UnauthorizedAccessException(
                $"El tipo de movimiento '{tipo.nombre}' requiere autorización. " +
                "Necesita el permiso de autorizar movimientos sensibles para usarlo.");
        }

        var tipoMotor = TipoMovimientoDe(tipo.clase);

        var bodega = await _context.alm_bodegas.AsNoTracking()
            .FirstOrDefaultAsync(b => b.id == dto.BodegaId, ct)
            ?? throw new InvalidOperationException("La bodega no existe en la empresa actual.");
        if (!bodega.activo)
            throw new InvalidOperationException($"La bodega '{bodega.nombre}' está inactiva.");

        ValidarRenglones(tipo.clase, dto.Detalles);

        // Resuelve los pares (artículo, bodega) ANTES de abrir la transacción y ORDENADOS por
        // id: el motor bloquea en el orden que se le pasa, así que un orden estable evita el
        // deadlock cruzado entre dos documentos que toquen los mismos artículos.
        var pares = await ResolverParesAsync(dto.Detalles, dto.BodegaId, ct);

        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var numero = await SiguienteNumeroAsync(companyId, ct);
        var hdrUuid = dto.Uuid ?? Guid.NewGuid();

        var cabecera = new alm_movimiento_hdr
        {
            numero = numero,
            tipo_movimiento_id = tipo.id,
            bodega_id = dto.BodegaId,
            fecha = fecha,
            motivo = motivo,
            documento_referencia = Limpiar(dto.DocumentoReferencia, 30),
            observaciones = Limpiar(dto.Observaciones, 1000),
            total = 0m,
            estado = EstadoMovimientoAlmacen.Registrado,
            posteado = false,
            uuid = hdrUuid,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        // El uuid del renglón se deriva de (documento, posición), NO de su id: el id no existe
        // hasta después del INSERT, y como el índice único es (company_id, uuid), insertar varias
        // líneas con un uuid provisional común colisionaría. La posición es estable porque las
        // líneas van ordenadas por par, así que un reintento produce los mismos uuid.
        var lineas = dto.Detalles
            .OrderBy(d => pares[d.ArticuloId].ParId)
            .Select((d, indice) => new alm_movimiento_dtl
            {
                cabecera = cabecera,
                articulo_id = d.ArticuloId,
                codigo_articulo = pares[d.ArticuloId].Codigo,
                cantidad = d.Cantidad,
                // En SALIDA el costo tecleado se descarta: manda el promedio vigente.
                costo_unitario = tipo.clase == ClaseMovimientoInventario.Salida ? 0m : d.CostoUnitario,
                uuid = UuidV5.CreateInventario($"movimiento|{companyId}|{hdrUuid}|{indice}"),
                posteado = false
            })
            .ToList();

        _context.alm_movimiento_hdrs.Add(cabecera);
        _context.alm_movimiento_dtls.AddRange(lineas);
        // Los renglones necesitan su id — ES el documento_id del asiento.
        await _context.SaveChangesAsync(ct);

        decimal total = 0m;

        foreach (var linea in lineas)
        {
            var resultado = await _posting.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = tipoMotor,
                ArticuloBodegaId = pares[linea.articulo_id].ParId,
                Cantidad = linea.cantidad,
                CostoUnitario = linea.costo_unitario,
                Fecha = fecha,
                // El vocabulario cerrado del kardex NO cambia: para el libro sigue siendo un
                // ajuste. Quién lo originó vive en tipo_movimiento_id y en la observación.
                DocumentoTipo = TipoDocumentoInventario.Ajuste,
                DocumentoId = linea.id,
                Observacion = $"{tipo.codigo} · {motivo}"
            }, user, ct);

            linea.kardex_id = resultado.KardexId;
            linea.costo_real = linea.costo_unitario > 0m
                ? linea.costo_unitario
                : resultado.CostoPromedioResultante;
            linea.total = Math.Round(linea.cantidad * (linea.costo_real ?? 0m), 2, MidpointRounding.AwayFromZero);
            linea.posteado = true;
            total += linea.total;
        }

        cabecera.total = Math.Round(total, 2, MidpointRounding.AwayFromZero);
        cabecera.posteado = true;
        cabecera.fecha_posteo = ahora;

        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return (await GetByIdAsync(cabecera.id, ct))!;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Anulación
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<bool> AnularAsync(int id, string motivo, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        var motivoAnulacion = (motivo ?? string.Empty).Trim();
        if (motivoAnulacion.Length == 0)
            throw new InvalidOperationException("Debe indicar el motivo de la anulación.");
        if (motivoAnulacion.Length > 500)
            throw new InvalidOperationException("El motivo de anulación no puede superar los 500 caracteres.");

        var cabecera = await _context.alm_movimiento_hdrs
            .Include(h => h.lineas)
            .FirstOrDefaultAsync(h => h.id == id, ct);
        if (cabecera is null) return false;

        // Idempotente: anular dos veces no revierte dos veces.
        if (cabecera.estado == EstadoMovimientoAlmacen.Anulado) return true;

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var posteadas = cabecera.lineas.Where(l => l.posteado && l.kardex_id.HasValue).ToList();

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // Antes de tocar nada: si el movimiento fue una ENTRADA, la mercadería tiene que
        // seguir estando. Si ya salió, la reversa dejaría existencia negativa y eso no se
        // arregla anulando. Se comprueba TODO antes de revertir NADA.
        foreach (var linea in posteadas)
        {
            var asiento = await _context.alm_kardexs.AsNoTracking()
                .FirstOrDefaultAsync(k => k.id == linea.kardex_id!.Value, ct);
            if (asiento is null) continue;

            if (asiento.ingresos > 0m)
            {
                var fila = await _context.alm_articulo_bodegas.AsNoTracking()
                    .FirstOrDefaultAsync(u => u.articulo_id == linea.articulo_id
                                           && u.bodega_id == cabecera.bodega_id, ct);
                var disponible = fila?.existencia ?? 0m;
                if (disponible < asiento.ingresos)
                {
                    throw new InvalidOperationException(
                        $"No se puede anular: del artículo {linea.codigo_articulo ?? linea.articulo_id.ToString()} " +
                        $"entraron {asiento.ingresos:0.####} y sólo quedan {disponible:0.####} en la bodega. " +
                        "La mercadería ya salió; corrija con un movimiento de salida.");
                }
            }
        }

        foreach (var linea in posteadas)
        {
            var asiento = await _context.alm_kardexs.AsNoTracking()
                .FirstOrDefaultAsync(k => k.id == linea.kardex_id!.Value, ct);
            if (asiento is null) continue;

            var fila = await _context.alm_articulo_bodegas.AsNoTracking()
                .FirstOrDefaultAsync(u => u.articulo_id == linea.articulo_id
                                       && u.bodega_id == cabecera.bodega_id, ct);
            if (fila is null) continue;

            // El uuid de la reversa lo deriva el motor del asiento revertido, así que
            // reintentar la anulación no duplica contra-asientos.
            await _posting.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.Reversa,
                ArticuloBodegaId = fila.id,
                Cantidad = linea.cantidad,
                CostoUnitario = linea.costo_real ?? linea.costo_unitario,
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                KardexIdRevertido = asiento.id,
                Observacion = $"Anulación del movimiento {cabecera.numero:00000}"
            }, user, ct);
        }

        cabecera.estado = EstadoMovimientoAlmacen.Anulado;
        cabecera.anulado_por = usuario;
        cabecera.fecha_anulacion = ahora;
        cabecera.motivo_anulacion = motivoAnulacion;
        cabecera.usuariomodificacion = usuario;
        cabecera.fechamodificacion = ahora;

        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Apoyo
    // ─────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reglas por clase, en el idioma del documento. El motor vuelve a validar; esto es para
    /// que el mensaje hable de renglones y no de asientos.
    /// </summary>
    private static void ValidarRenglones(string clase, List<MovimientoAlmacenDetalleDto> detalles)
    {
        for (var i = 0; i < detalles.Count; i++)
        {
            var d = detalles[i];
            var n = i + 1;

            if (d.ArticuloId <= 0)
                throw new InvalidOperationException($"El renglón {n} no tiene artículo.");

            switch (clase)
            {
                case ClaseMovimientoInventario.Entrada:
                    if (d.Cantidad <= 0m)
                        throw new InvalidOperationException($"El renglón {n}: la cantidad debe ser mayor que cero.");
                    if (d.CostoUnitario <= 0m)
                        throw new InvalidOperationException(
                            $"El renglón {n}: una entrada debe indicar el costo unitario de lo que ingresa.");
                    break;

                case ClaseMovimientoInventario.Salida:
                    if (d.Cantidad <= 0m)
                        throw new InvalidOperationException($"El renglón {n}: la cantidad debe ser mayor que cero.");
                    // El costo NO se exige: la salida se valoriza al promedio vigente.
                    break;

                case ClaseMovimientoInventario.Valor:
                    if (d.Cantidad != 0m)
                        throw new InvalidOperationException(
                            $"El renglón {n}: una corrección de valor no mueve existencia, la cantidad debe ser 0.");
                    if (d.CostoUnitario <= 0m)
                        throw new InvalidOperationException(
                            $"El renglón {n}: una corrección de valor debe indicar el costo corregido.");
                    break;
            }
        }

        var repetido = detalles.GroupBy(d => d.ArticuloId).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
        {
            throw new InvalidOperationException(
                $"El artículo {repetido.First().CodigoArticulo ?? repetido.Key.ToString()} está en más de un renglón. " +
                "Únalos en uno solo: dos renglones del mismo par se bloquearían entre sí al postear.");
        }
    }

    private sealed record ParArticulo(int ParId, string? Codigo);

    /// <summary>
    /// Resuelve el par (artículo, bodega) de cada renglón. A diferencia de compras, un
    /// movimiento NO da de alta ubicaciones: si el artículo no tiene ubicación en esa bodega,
    /// se rechaza (mismo criterio que <c>AjusteInventarioService</c>).
    /// </summary>
    private async Task<Dictionary<int, ParArticulo>> ResolverParesAsync(
        List<MovimientoAlmacenDetalleDto> detalles, int bodegaId, CancellationToken ct)
    {
        var articuloIds = detalles.Select(d => d.ArticuloId).Distinct().ToList();

        var filas = await (from u in _context.alm_articulo_bodegas.AsNoTracking()
                           join a in _context.alm_articulos.AsNoTracking() on u.articulo_id equals a.id
                           where u.bodega_id == bodegaId && articuloIds.Contains(u.articulo_id)
                           select new { u.id, u.articulo_id, u.activo, a.codigo_articulo })
                          .ToListAsync(ct);

        var mapa = new Dictionary<int, ParArticulo>();
        foreach (var articuloId in articuloIds)
        {
            var fila = filas.FirstOrDefault(f => f.articulo_id == articuloId);
            if (fila is null)
            {
                var codigo = detalles.First(d => d.ArticuloId == articuloId).CodigoArticulo ?? articuloId.ToString();
                throw new InvalidOperationException(
                    $"El artículo {codigo} no tiene ubicación en la bodega seleccionada. " +
                    "Créela desde la ficha del artículo antes de moverlo.");
            }
            if (!fila.activo)
            {
                throw new InvalidOperationException(
                    $"La ubicación del artículo {fila.codigo_articulo} en esa bodega está deshabilitada: reactívela antes de moverla.");
            }
            mapa[articuloId] = new ParArticulo(fila.id, fila.codigo_articulo);
        }

        return mapa;
    }

    /// <summary>
    /// Siguiente número de la empresa: alta idempotente del contador y luego
    /// <c>SELECT … FOR UPDATE</c> para serializar dos altas concurrentes. El
    /// <c>company_id</c> va DENTRO del SQL crudo por el filtro de tenant de EF.
    /// </summary>
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

    /// <summary>
    /// Traduce la clase del catálogo al vocabulario cerrado del motor. Es el MISMO mapeo de
    /// <c>AjusteInventarioService.TipoMovimientoDe</c>: la clase es la capa que el motor sabe
    /// ejecutar, el tipo es sólo el nombre de negocio.
    /// </summary>
    private static TipoMovimientoInventario TipoMovimientoDe(string clase) => clase switch
    {
        ClaseMovimientoInventario.Entrada => TipoMovimientoInventario.AjustePositivo,
        ClaseMovimientoInventario.Salida => TipoMovimientoInventario.AjusteNegativo,
        ClaseMovimientoInventario.Valor => TipoMovimientoInventario.AjusteValor,
        // TRASLADO no llega aquí: CrearYPostearAsync lo rechaza antes con un mensaje accionable.
        _ => throw new InvalidOperationException(
            $"La clase '{clase}' no la maneja este documento. Clases válidas aquí: ENTRADA, SALIDA, VALOR.")
    };
}
