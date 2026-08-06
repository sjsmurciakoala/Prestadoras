using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Kardex de movimientos de bodega (alm_kardex). El saldo corrido se calcula
/// sobre todos los movimientos del artículo ordenados cronológicamente; el
/// filtro de fecha/tipo sólo recorta las filas mostradas, no el saldo histórico.
/// La bodega, en cambio, delimita el universo del kardex: al filtrar por bodega
/// el saldo corrido pasa a ser el de esa bodega (kardex por bodega).
/// </summary>
public sealed class KardexService : IKardexService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public KardexService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    public async Task<KardexArticuloDto?> GetByArticuloAsync(KardexFilterDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // Resolver el artículo por id (preferente) o por código (compatibilidad).
        var articuloQuery = _context.alm_articulos.AsNoTracking();
        var codigoFiltro = filtro.CodigoArticulo?.Trim();

        var articulo = filtro.ArticuloId.HasValue
            ? await articuloQuery.Where(a => a.id == filtro.ArticuloId.Value)
                .Select(a => new
                {
                    a.id,
                    a.codigo_articulo,
                    a.descripcion,
                    a.unidad_medida,
                    // La unidad real vive en el catálogo (unidad_medida_id). La columna
                    // de texto unidad_medida es legacy SIMAFI y viene NULL en los
                    // artículos nuevos, así que se usa solo como respaldo.
                    UnidadCodigo = a.unidad_medida_ref != null ? a.unidad_medida_ref.codigo : null,
                    a.existencia
                })
                .FirstOrDefaultAsync(ct)
            : !string.IsNullOrWhiteSpace(codigoFiltro)
                ? await articuloQuery.Where(a => a.codigo_articulo == codigoFiltro)
                    .Select(a => new
                {
                    a.id,
                    a.codigo_articulo,
                    a.descripcion,
                    a.unidad_medida,
                    // La unidad real vive en el catálogo (unidad_medida_id). La columna
                    // de texto unidad_medida es legacy SIMAFI y viene NULL en los
                    // artículos nuevos, así que se usa solo como respaldo.
                    UnidadCodigo = a.unidad_medida_ref != null ? a.unidad_medida_ref.codigo : null,
                    a.existencia
                })
                    .FirstOrDefaultAsync(ct)
                : null;

        if (articulo is null)
        {
            return null;
        }

        var articuloId = articulo.id;
        var codigoRef = articulo.codigo_articulo;
        var tieneCodigo = !string.IsNullOrWhiteSpace(codigoRef);

        // Universo del kardex: movimientos del artículo por articulo_id, con fallback
        // al código para los movimientos aún no re-enlazados (transición/huérfanos).
        // Opcionalmente acotados a una bodega, que delimita el saldo corrido (a
        // diferencia de los filtros de fecha/tipo, que sólo recortan la presentación).
        var query = _context.alm_kardexs
            .AsNoTracking()
            .Where(k => k.articulo_id == articuloId
                     || (tieneCodigo && k.articulo_id == null && k.codigo_articulo == codigoRef));

        if (filtro.BodegaId.HasValue)
        {
            query = query.Where(k => k.bodega_id == filtro.BodegaId.Value);
        }

        // Orden cronológico (id como desempate estable para la misma fecha).
        var movimientos = await query
            .OrderBy(k => k.fecha)
            .ThenBy(k => k.id)
            .Select(k => new KardexMovimientoDto
            {
                Id = k.id,
                Fecha = k.fecha,
                NumeroDocumento = k.numero_documento,
                TipoTransaccion = k.tipo_transaccion,
                Descripcion = k.descripcion,
                Departamento = k.departamento_desc,
                BodegaId = k.bodega_id,
                BodegaCodigo = k.bodega_ref != null ? k.bodega_ref.codigo : null,
                BodegaNombre = k.bodega_ref != null ? k.bodega_ref.nombre : null,
                Ingresos = k.ingresos,
                Salidas = k.salidas,
                ValorUnitario = k.valor_unitario,
                Total = k.total,
                UsuarioCreacion = k.usuariocreacion,
                FechaCreacion = k.fechacreacion,
                DocumentoTipo = k.documento_tipo,
                DocumentoId = k.documento_id,
                ExistenciaResultante = k.existencia_resultante,
                CostoPromedioResultante = k.costo_promedio_resultante
            })
            .ToListAsync(ct);

        var saldoCalculado = AplicarPuntoDeCorte(movimientos);

        // Filtro de presentación (no afecta el saldo histórico ya calculado).
        IEnumerable<KardexMovimientoDto> filtrados = movimientos;

        if (filtro.FechaDesde.HasValue)
        {
            filtrados = filtrados.Where(m => m.Fecha.HasValue && m.Fecha.Value >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            filtrados = filtrados.Where(m => m.Fecha.HasValue && m.Fecha.Value <= filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoTransaccion))
        {
            var tipo = filtro.TipoTransaccion.Trim();
            filtrados = filtrados.Where(m => m.TipoTransaccion == tipo);
        }

        var lista = filtrados.ToList();

        // Existencia del ámbito consultado. Con bodega filtrada el saldo corrido es el de
        // ESA bodega, así que la cifra comparable es alm_articulo_bodega.existencia (fila
        // ACTIVA, mismo contrato de rollup que el maestro) y no alm_articulo.existencia,
        // que es el total del artículo. Sin fila activa se devuelve null: no hay con qué
        // comparar (evita reportar descuadre por ausencia de rollup).
        decimal? existenciaBodega = null;
        if (filtro.BodegaId.HasValue)
        {
            existenciaBodega = await _context.alm_articulo_bodegas
                .AsNoTracking()
                .Where(u => u.articulo_id == articuloId
                         && u.bodega_id == filtro.BodegaId.Value
                         && u.activo)
                .Select(u => (decimal?)u.existencia)
                .FirstOrDefaultAsync(ct);
        }

        return new KardexArticuloDto
        {
            Codigo = articulo.codigo_articulo,
            Descripcion = articulo.descripcion,
            UnidadMedida = articulo.UnidadCodigo ?? articulo.unidad_medida,
            ExistenciaRegistrada = articulo.existencia,
            SaldoCalculado = saldoCalculado,
            BodegaId = filtro.BodegaId,
            ExistenciaBodega = existenciaBodega,
            TotalIngresos = lista.Sum(m => m.Ingresos),
            TotalSalidas = lista.Sum(m => m.Salidas),
            Movimientos = lista
        };
    }

    /// <summary>
    /// Aplica el PUNTO DE CORTE al saldo corrido y devuelve el saldo final.
    /// <para>
    /// Sin esto, postear la carga inicial <b>empeoraría</b> la pantalla: el saldo sumaría
    /// los ~47 mil asientos migrados de SIMAFI MÁS la apertura, y nunca cuadraría contra la
    /// existencia registrada. La regla: el saldo <b>arranca en cero en el asiento de carga
    /// inicial</b> de cada par (artículo, bodega); todo lo anterior es histórico informativo
    /// y se devuelve con <c>Saldo = null</c>.
    /// </para>
    /// <para>
    /// Compatibilidad hacia atrás: un par SIN carga inicial se comporta como siempre —el
    /// saldo corre desde el primer movimiento—, que es lo que mantiene usable la pantalla
    /// durante la transición, mientras unos pares ya tienen apertura y otros no.
    /// </para>
    /// </summary>
    private static decimal AplicarPuntoDeCorte(List<KardexMovimientoDto> movimientos)
    {
        // El corte es POR PAR (artículo, bodega): en una consulta sin filtro de bodega
        // conviven varios pares y cada uno tiene su propia apertura.
        // Los movimientos ya vienen en orden cronológico (fecha, id).
        var corteDeBodega = new Dictionary<int, int>();
        foreach (var m in movimientos)
        {
            if (m.DocumentoTipo != TipoDocumentoInventario.CargaInicial || m.Ingresos <= 0)
            {
                continue;
            }

            // La apertura vigente es la PRIMERA no revertida. Una apertura revertida tiene
            // su REVERSA en la lista; ambas quedan del lado pre-corte y no se cuentan dos veces.
            var clave = m.BodegaId ?? 0;
            var revertida = movimientos.Any(r =>
                r.DocumentoTipo == TipoDocumentoInventario.Reversa && r.DocumentoId == m.Id);

            if (!revertida && !corteDeBodega.ContainsKey(clave))
            {
                corteDeBodega[clave] = m.Id;
                m.EsLineaDeCorte = true;
            }
        }

        var saldoPorBodega = new Dictionary<int, decimal>();
        var alcanzoCorte = new Dictionary<int, bool>();
        decimal saldoTotal = 0m;

        foreach (var m in movimientos)
        {
            var clave = m.BodegaId ?? 0;
            var tieneCorte = corteDeBodega.TryGetValue(clave, out var corteId);

            if (tieneCorte)
            {
                if (!alcanzoCorte.GetValueOrDefault(clave))
                {
                    if (m.Id == corteId)
                    {
                        // La línea de corte ABRE el saldo: arranca en cero y suma su apertura.
                        alcanzoCorte[clave] = true;
                    }
                    else
                    {
                        // Anterior al corte: histórico informativo, sin saldo.
                        m.EsPreCorte = true;
                        m.Saldo = null;
                        continue;
                    }
                }
            }

            var saldo = saldoPorBodega.GetValueOrDefault(clave) + m.Ingresos - m.Salidas;
            saldoPorBodega[clave] = saldo;
            m.Saldo = saldo;
        }

        // El saldo comparable contra la existencia es la suma de los saldos por bodega
        // (con una sola bodega filtrada, es el de esa bodega).
        foreach (var s in saldoPorBodega.Values)
        {
            saldoTotal += s;
        }

        return saldoTotal;
    }

    public async Task<IReadOnlyList<TipoMovimientoDto>> GetTiposMovimientoAsync(CancellationToken ct = default)
    {
        var codigos = await _context.alm_kardexs
            .AsNoTracking()
            .Where(k => k.tipo_transaccion != null && k.tipo_transaccion != "")
            .Select(k => k.tipo_transaccion!)
            .Distinct()
            .OrderBy(t => t)
            .ToListAsync(ct);

        return codigos
            .Select(c => new TipoMovimientoDto { Codigo = c, Descripcion = TipoMovimientoKardex.Describir(c) })
            .ToList();
    }

    // ── Libro de movimientos de bodega ───────────────────────────────────────
    // A diferencia del kardex por artículo, aquí el eje es la BODEGA: una página de todos
    // los asientos de la bodega (cualquier artículo). No hay saldo corrido "mezclado" —cada
    // fila trae su existencia_resultante materializada—, así que se pagina en el servidor
    // sin recomputar nada (el kardex de una bodega puede tener decenas de miles de filas).

    public async Task<PagedResult<MovimientoBodegaItemDto>> GetMovimientosBodegaPagedAsync(
        MovimientosBodegaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        // La bodega es el eje del libro: sin ella no hay universo que paginar.
        if (!filtro.BodegaId.HasValue)
        {
            return new PagedResult<MovimientoBodegaItemDto>();
        }

        var query = AplicarFiltrosBodega(filtro);
        var total = await query.CountAsync(ct);

        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : take;

        var items = await AplicarOrdenBodega(query, sortField, sortDesc)
            .Skip(skip)
            .Take(take)
            .Select(ProyeccionBodega())
            .ToListAsync(ct);

        return new PagedResult<MovimientoBodegaItemDto>(items, total);
    }

    public async Task<MovimientosBodegaResumenDto> GetResumenBodegaAsync(MovimientosBodegaFilterDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        if (!filtro.BodegaId.HasValue)
        {
            return new MovimientosBodegaResumenDto();
        }

        // Agregados en el servidor sobre TODO el universo filtrado (no la página visible).
        // Consultas separadas (como el resumen del maestro) en vez de GroupBy: garantiza la
        // traducción a SQL. El (decimal?) ... ?? 0m evita el fallo de Sum sobre secuencia vacía.
        var query = AplicarFiltrosBodega(filtro);

        var totalMovimientos = await query.CountAsync(ct);
        var totalIngresos = await query.SumAsync(k => (decimal?)k.ingresos, ct) ?? 0m;
        var totalSalidas = await query.SumAsync(k => (decimal?)k.salidas, ct) ?? 0m;
        var valorMovido = await query.SumAsync(k => (decimal?)k.total, ct) ?? 0m;

        return new MovimientosBodegaResumenDto
        {
            TotalMovimientos = totalMovimientos,
            TotalIngresos = totalIngresos,
            TotalSalidas = totalSalidas,
            ValorMovido = valorMovido
        };
    }

    /// <summary>Universo del libro: asientos de la bodega, acotados por período/tipo/artículo/búsqueda.</summary>
    private IQueryable<alm_kardex> AplicarFiltrosBodega(MovimientosBodegaFilterDto filtro)
    {
        var query = _context.alm_kardexs
            .AsNoTracking()
            .Where(k => k.bodega_id == filtro.BodegaId!.Value);

        if (filtro.ArticuloId.HasValue)
        {
            query = query.Where(k => k.articulo_id == filtro.ArticuloId.Value);
        }

        if (filtro.FechaDesde.HasValue)
        {
            query = query.Where(k => k.fecha != null && k.fecha >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            query = query.Where(k => k.fecha != null && k.fecha <= filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoTransaccion))
        {
            var tipo = filtro.TipoTransaccion.Trim();
            query = query.Where(k => k.tipo_transaccion == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(k =>
                    EF.Functions.ILike(k.codigo_articulo ?? string.Empty, likePattern)
                    || EF.Functions.ILike(k.descripcion ?? string.Empty, likePattern)
                    || (k.articulo_ref != null && (
                        EF.Functions.ILike(k.articulo_ref.codigo_articulo ?? string.Empty, likePattern)
                        || EF.Functions.ILike(k.articulo_ref.descripcion, likePattern))));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(k =>
                    (k.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered)
                    || (k.descripcion ?? string.Empty).ToLowerInvariant().Contains(lowered)
                    || (k.articulo_ref != null && (
                        (k.articulo_ref.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered)
                        || k.articulo_ref.descripcion.ToLowerInvariant().Contains(lowered))));
            }
        }

        return query;
    }

    /// <summary>Orden del grid remoto. Por defecto: cronológico DESCENDENTE (lo más reciente primero).</summary>
    private static IQueryable<alm_kardex> AplicarOrdenBodega(IQueryable<alm_kardex> query, string? sortField, bool sortDesc)
        => sortField switch
        {
            nameof(MovimientoBodegaItemDto.Fecha) => sortDesc
                ? query.OrderByDescending(k => k.fecha).ThenByDescending(k => k.id)
                : query.OrderBy(k => k.fecha).ThenBy(k => k.id),
            nameof(MovimientoBodegaItemDto.ArticuloDescripcion) => sortDesc
                ? query.OrderByDescending(k => k.articulo_ref!.descripcion).ThenByDescending(k => k.id)
                : query.OrderBy(k => k.articulo_ref!.descripcion).ThenBy(k => k.id),
            nameof(MovimientoBodegaItemDto.Ingresos) => sortDesc
                ? query.OrderByDescending(k => k.ingresos).ThenByDescending(k => k.id)
                : query.OrderBy(k => k.ingresos).ThenBy(k => k.id),
            nameof(MovimientoBodegaItemDto.Salidas) => sortDesc
                ? query.OrderByDescending(k => k.salidas).ThenByDescending(k => k.id)
                : query.OrderBy(k => k.salidas).ThenBy(k => k.id),
            nameof(MovimientoBodegaItemDto.Total) => sortDesc
                ? query.OrderByDescending(k => k.total).ThenByDescending(k => k.id)
                : query.OrderBy(k => k.total).ThenBy(k => k.id),
            _ => query.OrderByDescending(k => k.fecha).ThenByDescending(k => k.id)
        };

    private static Expression<Func<alm_kardex, MovimientoBodegaItemDto>> ProyeccionBodega() => k => new MovimientoBodegaItemDto
    {
        Id = k.id,
        Fecha = k.fecha,
        NumeroDocumento = k.numero_documento,
        TipoTransaccion = k.tipo_transaccion,
        ArticuloId = k.articulo_id,
        // Código del catálogo si está enlazado; si no, el snapshot SIMAFI (histórico sin re-enlazar).
        ArticuloCodigo = k.articulo_ref != null ? k.articulo_ref.codigo_articulo : k.codigo_articulo,
        ArticuloDescripcion = k.articulo_ref != null ? k.articulo_ref.descripcion : null,
        Descripcion = k.descripcion,
        Ingresos = k.ingresos,
        Salidas = k.salidas,
        ValorUnitario = k.valor_unitario,
        Total = k.total,
        ExistenciaResultante = k.existencia_resultante,
        CostoPromedioResultante = k.costo_promedio_resultante,
        DocumentoTipo = k.documento_tipo,
        DocumentoId = k.documento_id,
        UsuarioCreacion = k.usuariocreacion,
        FechaCreacion = k.fechacreacion
    };

    // ── Impresión (PDF) del kardex ───────────────────────────────────────────

    public async Task<IReadOnlyList<MovimientoBodegaItemDto>> GetMovimientosBodegaAsync(
        MovimientosBodegaFilterDto filtro, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);
        if (!filtro.BodegaId.HasValue)
        {
            return new List<MovimientoBodegaItemDto>();
        }

        // Para el PDF (libro imprimible) el orden es cronológico ASCENDENTE, y sin paginar.
        return await AplicarFiltrosBodega(filtro)
            .OrderBy(k => k.fecha).ThenBy(k => k.id)
            .Select(ProyeccionBodega())
            .ToListAsync(ct);
    }

    public async Task<MovimientosKardexImpresionDto> GetDatosImpresionArticuloAsync(
        KardexFilterDto filtro, string impresoPor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var empresa = await CargarEmpresaAsync(ct);
        var kardex = await GetByArticuloAsync(filtro, ct);

        if (kardex is null)
        {
            return BuildImpresion(empresa, impresoPor, "KARDEX POR ARTÍCULO", "Artículo no encontrado",
                "Bodega", string.Empty, new List<MovimientoKardexImpresionRow>(), 0m, 0m);
        }

        var filas = kardex.Movimientos.Select(x => new MovimientoKardexImpresionRow
        {
            Fecha = x.Fecha.HasValue ? x.Fecha.Value.ToString("dd/MM/yyyy") : "—",
            Documento = x.NumeroDocumento.HasValue ? x.NumeroDocumento.Value.ToString("0") : "—",
            Tipo = x.TipoDescripcion,
            Contexto = string.IsNullOrWhiteSpace(x.BodegaCodigo) ? (x.BodegaNombre ?? "—") : x.BodegaCodigo,
            Descripcion = x.Descripcion ?? string.Empty,
            Entradas = x.Ingresos,
            Salidas = x.Salidas,
            ValorUnitario = x.ValorUnitario,
            Saldo = x.Saldo
        }).ToList();

        var subtitulo = string.IsNullOrWhiteSpace(kardex.Codigo) ? kardex.Descripcion : $"{kardex.Codigo} — {kardex.Descripcion}";
        var bodegaTxt = filtro.BodegaId.HasValue
            ? (kardex.Movimientos.FirstOrDefault(m => m.BodegaId == filtro.BodegaId)?.BodegaNombre ?? "bodega filtrada")
            : "todas las bodegas";
        var filtroTexto = $"{PeriodoTexto(filtro.FechaDesde, filtro.FechaHasta)} · {bodegaTxt}";

        return BuildImpresion(empresa, impresoPor, "KARDEX POR ARTÍCULO", subtitulo,
            "Bodega", filtroTexto, filas, kardex.TotalIngresos, kardex.TotalSalidas);
    }

    public async Task<MovimientosKardexImpresionDto> GetDatosImpresionBodegaAsync(
        MovimientosBodegaFilterDto filtro, string impresoPor, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(filtro);

        var empresa = await CargarEmpresaAsync(ct);
        var movs = await GetMovimientosBodegaAsync(filtro, ct);

        var bodega = filtro.BodegaId.HasValue
            ? await _context.alm_bodegas.AsNoTracking()
                .Where(b => b.id == filtro.BodegaId.Value)
                .Select(b => new { b.codigo, b.nombre })
                .FirstOrDefaultAsync(ct)
            : null;
        var subtitulo = bodega != null ? $"Bodega: {bodega.codigo} — {bodega.nombre}" : "Bodega";

        var filas = movs.Select(x => new MovimientoKardexImpresionRow
        {
            Fecha = x.Fecha.HasValue ? x.Fecha.Value.ToString("dd/MM/yyyy") : "—",
            Documento = x.NumeroDocumento.HasValue ? x.NumeroDocumento.Value.ToString("0") : "—",
            Tipo = x.TipoDescripcion,
            Contexto = string.IsNullOrWhiteSpace(x.ArticuloCodigo) ? "—" : x.ArticuloCodigo,
            Descripcion = x.ArticuloDescripcion ?? string.Empty,
            Entradas = x.Ingresos,
            Salidas = x.Salidas,
            ValorUnitario = x.ValorUnitario,
            Saldo = x.ExistenciaResultante
        }).ToList();

        return BuildImpresion(empresa, impresoPor, "MOVIMIENTOS POR BODEGA", subtitulo,
            "Artículo", PeriodoTexto(filtro.FechaDesde, filtro.FechaHasta), filas,
            movs.Sum(x => x.Ingresos), movs.Sum(x => x.Salidas));
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }

    private static string PeriodoTexto(DateOnly? desde, DateOnly? hasta)
    {
        if (desde.HasValue && hasta.HasValue) return $"Del {desde.Value:dd/MM/yyyy} al {hasta.Value:dd/MM/yyyy}";
        if (desde.HasValue) return $"Desde {desde.Value:dd/MM/yyyy}";
        if (hasta.HasValue) return $"Hasta {hasta.Value:dd/MM/yyyy}";
        return "Todas las fechas";
    }

    private static MovimientosKardexImpresionDto BuildImpresion(
        cfg_company? empresa, string impresoPor, string titulo, string subtitulo,
        string columnaContexto, string filtroTexto, List<MovimientoKardexImpresionRow> filas,
        decimal totalEntradas, decimal totalSalidas)
        => new()
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),
            Titulo = titulo,
            Subtitulo = subtitulo,
            ColumnaContexto = columnaContexto,
            FiltroTexto = filtroTexto,
            Filas = filas,
            TotalEntradas = totalEntradas,
            TotalSalidas = totalSalidas
        };
}
