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

    public async Task<IReadOnlyList<string>> GetLetrasAsync(CancellationToken ct = default)
    {
        return await _context.letras
            .AsNoTracking()
            .Where(l => l.letras != null && l.letras.Trim() != string.Empty)
            .OrderBy(l => l.num)
            .ThenBy(l => l.letras)
            .Select(l => l.letras!)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LetraServicioLookupDto>> GetLetrasTarifaAsync(int tipoUsoCodigo, int categoriaId, CancellationToken ct = default)
    {
        var letrasFijas = _context.tarifas
            .AsNoTracking()
            .Where(t => t.tipo == tipoUsoCodigo && t.categoria_id == categoriaId)
            .Select(t => t.codigo);

        var letrasContador = _context.tarifas_contadors
            .AsNoTracking()
            .Where(t => t.tipo == tipoUsoCodigo
                        && t.categoria_id == categoriaId
                        && t.codigo != null
                        && t.codigo.Trim() != string.Empty)
            .Select(t => t.codigo!);

        var letras = await letrasFijas
            .Union(letrasContador)
            .Distinct()
            .OrderBy(codigo => codigo)
            .Select(codigo => new LetraServicioLookupDto(codigo, codigo))
            .ToListAsync(ct);

        return letras;
    }

    public async Task<IReadOnlyList<int>> GetCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default)
    {
        var categoriasTarifaFija = _context.tarifas
            .AsNoTracking()
            .Where(t => t.tipo == tipoUsoCodigo)
            .Select(t => t.categoria_id);

        var categoriasTarifaContador = _context.tarifas_contadors
            .AsNoTracking()
            .Where(t => t.tipo == tipoUsoCodigo && t.categoria_id.HasValue)
            .Select(t => t.categoria_id!.Value);

        var categoriasConfiguradas = categoriasTarifaFija
            .Union(categoriasTarifaContador);

        return await (from categoriaId in categoriasConfiguradas
                      join c in _context.categoria_servicios.AsNoTracking()
                          on categoriaId equals c.categoria_servicio_id
                      where c.estado
                      select c.categoria_servicio_id)
            .Distinct()
            .OrderBy(id => id)
            .ToListAsync(ct);
    }
}
