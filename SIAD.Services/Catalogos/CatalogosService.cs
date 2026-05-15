using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Catalogos;
using SIAD.Data;

namespace SIAD.Services.Catalogos;

public class CatalogosService : ICatalogosService
{
    private readonly SiadDbContext _context;

    public CatalogosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AbogadoLookupDto>> GetAbogadosAsync(CancellationToken ct = default)
    {
        return await _context.abogados
            .AsNoTracking()
            .Where(a => a.estado)
            .OrderBy(a => a.abogado_nombrecorto)
            .Select(a => new AbogadoLookupDto(
                a.abogado_id,
                a.abogado_codigo,
                !string.IsNullOrWhiteSpace(a.abogado_nombrecorto)
                    ? a.abogado_nombrecorto
                    : (a.abogado_nombrelargo ?? a.abogado_codigo)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BarrioLookupDto>> GetBarriosAsync(CancellationToken ct = default)
    {
        return await _context.barrios
            .AsNoTracking()
            .Where(b => b.estado)
            .OrderBy(b => b.descripcion)
            .Select(b => new BarrioLookupDto(
                b.barrio_codigo,
                b.descripcion))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServicioLookupDto>> GetServiciosAsync(CancellationToken ct = default)
    {
        return await _context.servicios
            .AsNoTracking()
            .Where(s => s.estado)
            .OrderBy(s => s.servicios_descripcioncorta)
            .Select(s => new ServicioLookupDto(
                s.servicios_id,
                s.servicios_codigo,
                s.servicios_descripcioncorta))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TipoUsoLookupDto>> GetTiposUsoAsync(CancellationToken ct = default)
    {
        return await _context.tipo_uso_servicios
            .AsNoTracking()
            .Where(t => t.estado)
            .OrderBy(t => t.descripcion)
            .Select(t => new TipoUsoLookupDto(
                t.tipo_uso_codigo,
                t.descripcion))
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<int>> GetCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
    }
}
