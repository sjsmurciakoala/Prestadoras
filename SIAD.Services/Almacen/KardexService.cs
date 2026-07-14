using Microsoft.EntityFrameworkCore;
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
                FechaCreacion = k.fechacreacion
            })
            .ToListAsync(ct);

        // Saldo corrido sobre TODOS los movimientos.
        decimal saldo = 0m;
        foreach (var m in movimientos)
        {
            saldo += m.Ingresos - m.Salidas;
            m.Saldo = saldo;
        }

        var saldoCalculado = saldo;

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

        return new KardexArticuloDto
        {
            Codigo = articulo.codigo_articulo,
            Descripcion = articulo.descripcion,
            UnidadMedida = articulo.UnidadCodigo ?? articulo.unidad_medida,
            ExistenciaRegistrada = articulo.existencia,
            SaldoCalculado = saldoCalculado,
            TotalIngresos = lista.Sum(m => m.Ingresos),
            TotalSalidas = lista.Sum(m => m.Salidas),
            Movimientos = lista
        };
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
