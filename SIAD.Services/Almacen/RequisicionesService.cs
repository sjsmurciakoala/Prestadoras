using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Requisiciones internas de materiales (alm_requisicion), sólo consulta.
/// Una requisición (numero) puede tener varias líneas; el grid es a nivel línea.
/// </summary>
public sealed class RequisicionesService : IRequisicionesService
{
    private readonly SiadDbContext _context;

    public RequisicionesService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RequisicionListItemDto>> GetAsync(RequisicionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new RequisicionFilterDto();

        var query = _context.alm_requisicions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Estatus))
        {
            var estatus = filtro.Estatus.Trim();
            query = query.Where(r => r.estatus == estatus);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Departamento))
        {
            var depto = filtro.Departamento.Trim();
            query = query.Where(r => r.departamento == depto);
        }

        if (filtro.FechaDesde.HasValue)
        {
            query = query.Where(r => r.fecha_requisicion != null && r.fecha_requisicion >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            query = query.Where(r => r.fecha_requisicion != null && r.fecha_requisicion <= filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(r =>
                    EF.Functions.ILike(r.solicitante ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(r.codigo_articulo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(r.descripcion ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(r.aplicacion ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(r =>
                    (r.solicitante ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (r.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (r.descripcion ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (r.aplicacion ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return await query
            .OrderByDescending(r => r.numero)
            .ThenBy(r => r.id)
            .Select(r => new RequisicionListItemDto
            {
                Id = r.id,
                Numero = r.numero,
                FechaRequisicion = r.fecha_requisicion,
                FechaEntrega = r.fecha_entrega,
                Solicitante = r.solicitante,
                Departamento = r.departamento,
                CodigoArticulo = r.codigo_articulo,
                Descripcion = r.descripcion,
                Aplicacion = r.aplicacion,
                Cantidad = r.cantidad,
                PrecioUnitario = r.precio_unitario,
                Total = r.total,
                Estatus = r.estatus,
                Aprobado = r.aprobado,
                Rechazado = r.rechazado,
                Descargado = r.descargado
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDepartamentosAsync(CancellationToken ct = default)
    {
        return await _context.alm_requisicions
            .AsNoTracking()
            .Where(r => r.departamento != null && r.departamento != "")
            .Select(r => r.departamento!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);
    }
}
