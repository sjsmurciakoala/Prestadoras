using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Registro de compras de almacén (alm_compra), sólo consulta. El alta de una
/// compra que afecte existencias/kardex es una operación aparte (posteo).
/// </summary>
public sealed class ComprasService : IComprasService
{
    private readonly SiadDbContext _context;

    public ComprasService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<CompraListItemDto>> GetAsync(CompraFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new CompraFilterDto();

        var query = _context.alm_compras.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Proveedor))
        {
            var proveedor = filtro.Proveedor.Trim();
            query = query.Where(c => c.proveedor == proveedor);
        }

        if (filtro.TipoCompra.HasValue)
        {
            query = query.Where(c => c.tipo_compra == filtro.TipoCompra.Value);
        }

        if (filtro.FechaDesde.HasValue)
        {
            query = query.Where(c => c.fecha != null && c.fecha >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            query = query.Where(c => c.fecha != null && c.fecha <= filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(c =>
                    EF.Functions.ILike(c.proveedor ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.codigo_articulo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.concepto ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.orden_compra ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(c =>
                    (c.proveedor ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.concepto ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.orden_compra ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return await query
            .OrderByDescending(c => c.fecha)
            .ThenByDescending(c => c.id)
            .Select(c => new CompraListItemDto
            {
                Id = c.id,
                ArticuloId = c.articulo_id,
                Fecha = c.fecha,
                FechaFactura = c.fecha_factura,
                Proveedor = c.proveedor,
                CodigoArticulo = c.codigo_articulo,
                Concepto = c.concepto,
                Cantidad = c.cantidad,
                PrecioUnitario = c.precio_unitario,
                Descuento = c.descuento,
                Total = c.total,
                NumeroFactura = c.numero_factura,
                OrdenCompra = c.orden_compra,
                Oficina = c.oficina,
                TipoCompra = c.tipo_compra,
                PlazoDias = c.plazo_dias
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetProveedoresAsync(CancellationToken ct = default)
    {
        return await _context.alm_compras
            .AsNoTracking()
            .Where(c => c.proveedor != null && c.proveedor != "")
            .Select(c => c.proveedor!)
            .Distinct()
            .OrderBy(p => p)
            .ToListAsync(ct);
    }
}
