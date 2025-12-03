using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Rutas;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Rutas;

public class RutasService : IRutasService
{
    private readonly SiadDbContext _context;

    public RutasService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<RutaListItemDto>> GetRutasAsync(RutaFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new RutaFilterDto();

        var query = _context.rutas.AsNoTracking().AsQueryable();

        if (filtro?.CodCiclo is int ciclo && ciclo > 0)
        {
            query = query.Where(r => r.codciclo == ciclo);
        }

        if (!string.IsNullOrWhiteSpace(filtro?.CodRuta))
        {
            var term = filtro!.CodRuta.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(r => EF.Functions.ILike(r.codruta ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(r => r.codruta != null && r.codruta.ToLower().Contains(lowered));
            }
        }

        return await query
            .OrderBy(r => r.codciclo)
            .ThenBy(r => r.codruta)
            .Select(r => new RutaListItemDto(
                r.id,
                r.codciclo ?? 0,
                r.codruta ?? string.Empty,
                r.descripcion))
            .ToListAsync(ct);
    }

    public async Task<RutaDetailDto?> GetRutaAsync(int id, CancellationToken ct = default)
    {
        return await _context.rutas
            .AsNoTracking()
            .Where(r => r.id == id)
            .Select(r => new RutaDetailDto(
                r.id,
                r.codciclo ?? 0,
                r.codruta ?? string.Empty,
                r.descripcion,
                r.codcicloNavigation != null ? r.codcicloNavigation.ciclos_descripcioncorta : null))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> CreateRutaAsync(RutaUpsertDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = new ruta
        {
            codciclo = dto.CodCiclo,
            codruta = NormalizeRequired(dto.CodRuta),
            descripcion = NormalizeOptional(dto.Descripcion)
        };

        _context.rutas.Add(entity);
        await _context.SaveChangesAsync(ct);

        return entity.id;
    }

    public async Task UpdateRutaAsync(int id, RutaUpsertDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.rutas.FirstOrDefaultAsync(r => r.id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro la ruta {id}.");
        }

        entity.codciclo = dto.CodCiclo;
        entity.codruta = NormalizeRequired(dto.CodRuta);
        entity.descripcion = NormalizeOptional(dto.Descripcion);

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<CicloLookupDto>> GetCiclosAsync(CancellationToken ct = default)
    {
        return await _context.ciclos
            .AsNoTracking()
            .Where(c => c.estado)
            .OrderBy(c => c.ciclos_descripcioncorta)
            .Select(c => new CicloLookupDto(c.ciclos_id, c.ciclos_descripcioncorta))
            .ToListAsync(ct);
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El valor no puede estar vacio.", nameof(value));
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
