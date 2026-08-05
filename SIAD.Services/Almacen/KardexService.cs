using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
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

    public KardexService(SiadDbContext context)
    {
        _context = context;
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
}
