using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Descargos (salidas/consumo) de almacén hacia departamentos (alm_descargo),
/// sólo consulta. Enlazan a la requisición de origen por numero_requisicion.
/// </summary>
public sealed class DescargosService : IDescargosService
{
    private readonly SiadDbContext _context;

    public DescargosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<DescargoListItemDto>> GetAsync(DescargoFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new DescargoFilterDto();

        var query = _context.alm_descargos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Departamento))
        {
            var depto = filtro.Departamento.Trim();
            query = query.Where(d => d.departamento == depto);
        }

        if (filtro.NumeroRequisicion.HasValue)
        {
            query = query.Where(d => d.numero_requisicion == filtro.NumeroRequisicion.Value);
        }

        if (filtro.FechaDesde.HasValue)
        {
            query = query.Where(d => d.fecha != null && d.fecha >= filtro.FechaDesde.Value);
        }

        if (filtro.FechaHasta.HasValue)
        {
            query = query.Where(d => d.fecha != null && d.fecha <= filtro.FechaHasta.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(d =>
                    EF.Functions.ILike(d.codigo_articulo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(d.comentario ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(d =>
                    (d.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (d.comentario ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return await query
            // Fechas nulas al final (en Postgres DESC pone NULLs primero).
            .OrderByDescending(d => d.fecha != null)
            .ThenByDescending(d => d.fecha)
            .ThenByDescending(d => d.id)
            .Select(d => new DescargoListItemDto
            {
                Id = d.id,
                ArticuloId = d.articulo_id,
                Fecha = d.fecha,
                CodigoArticulo = d.codigo_articulo,
                Cantidad = d.cantidad,
                PrecioUnitario = d.precio_unitario,
                Total = d.total,
                Departamento = d.departamento,
                NumeroRequisicion = d.numero_requisicion,
                NumeroDocumento = d.numero_documento,
                Comentario = d.comentario,
                Oficina = d.oficina
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetDepartamentosAsync(CancellationToken ct = default)
    {
        return await _context.alm_descargos
            .AsNoTracking()
            .Where(d => d.departamento != null && d.departamento != "")
            .Select(d => d.departamento!)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(ct);
    }
}
