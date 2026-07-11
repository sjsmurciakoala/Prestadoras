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

        var codigo = filtro.CodigoArticulo?.Trim();
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var articulo = await _context.alm_articulos
            .AsNoTracking()
            .Where(a => a.codigo_articulo == codigo)
            .Select(a => new { a.codigo_articulo, a.descripcion, a.unidad_medida, a.existencia })
            .FirstOrDefaultAsync(ct);

        if (articulo is null)
        {
            return null;
        }

        // Universo del kardex: movimientos del artículo, opcionalmente acotados a
        // una bodega. La bodega delimita el saldo corrido (a diferencia de los
        // filtros de fecha/tipo, que sólo recortan la presentación más abajo).
        var query = _context.alm_kardexs
            .AsNoTracking()
            .Where(k => k.codigo_articulo == codigo);

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
                Total = k.total
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
            UnidadMedida = articulo.unidad_medida,
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
