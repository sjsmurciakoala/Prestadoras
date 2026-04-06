using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
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
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(r => r.codciclo)
            .ThenBy(r => r.codruta)
            .Select(r => new RutaListItemDto(
                r.id,
                r.codciclo ?? 0,
                r.codruta ?? string.Empty,
                r.descripcion,
                r.estado))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<RutaListItemDto>> GetRutasPagedAsync(
        RutaFilterDto filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);
        var totalCount = await query.CountAsync(ct);
        query = ApplySort(query, sortField, sortDesc);

        if (skip < 0)
        {
            skip = 0;
        }

        if (take <= 0)
        {
            take = 50;
        }

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(r => new RutaListItemDto(
                r.id,
                r.codciclo ?? 0,
                r.codruta ?? string.Empty,
                r.descripcion,
                r.estado))
            .ToListAsync(ct);

        return new PagedResult<RutaListItemDto>(items, totalCount);
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
                r.codcicloNavigation != null ? r.codcicloNavigation.ciclos_descripcioncorta : null,
                r.estado))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<int> CreateRutaAsync(RutaUpsertDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeRequired(dto.CodRuta, 50, "codigo");
        var descripcion = NormalizeRequired(dto.Descripcion, 200, "descripcion");
        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var entity = new ruta
        {
            codciclo = dto.CodCiclo,
            codruta = codigo,
            descripcion = descripcion,
            estado = dto.Activo,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.rutas.Add(entity);
        await _context.SaveChangesAsync(ct);

        return entity.id;
    }

    public async Task UpdateRutaAsync(int id, RutaUpsertDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.rutas.FirstOrDefaultAsync(r => r.id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro la ruta {id}.");
        }

        entity.codciclo = dto.CodCiclo;
        entity.codruta = NormalizeRequired(dto.CodRuta, 50, "codigo");
        entity.descripcion = NormalizeRequired(dto.Descripcion, 200, "descripcion");
        entity.estado = dto.Activo;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeactivateRutaAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "La ruta no es valida.");
        }

        var entity = await _context.rutas.FirstOrDefaultAsync(r => r.id == id, ct);
        if (entity is null)
        {
            return false;
        }

        if (!entity.estado)
        {
            return true;
        }

        entity.estado = false;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);

        return true;
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

    private IQueryable<ruta> BuildQuery(RutaFilterDto? filtro)
    {
        filtro ??= new RutaFilterDto();

        var query = _context.rutas.AsNoTracking().AsQueryable();

        if (filtro.CodCiclo is int ciclo && ciclo > 0)
        {
            query = query.Where(r => r.codciclo == ciclo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.CodRuta))
        {
            var term = filtro.CodRuta.Trim();
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

        if (filtro.Activo.HasValue)
        {
            query = query.Where(r => r.estado == filtro.Activo.Value);
        }

        return query;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} no puede superar {maxLength} caracteres.", nameof(value));
        }

        return normalized;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }

    private static IQueryable<ruta> ApplySort(IQueryable<ruta> query, string? sortField, bool sortDesc)
    {
        if (string.IsNullOrWhiteSpace(sortField))
        {
            return query.OrderBy(r => r.codciclo).ThenBy(r => r.codruta);
        }

        var field = sortField.Trim();
        return field switch
        {
            nameof(RutaListItemDto.CodCiclo) or "codCiclo" or "codciclo" => sortDesc
                ? query.OrderByDescending(r => r.codciclo).ThenByDescending(r => r.codruta)
                : query.OrderBy(r => r.codciclo).ThenBy(r => r.codruta),
            nameof(RutaListItemDto.CodRuta) or "codRuta" or "codruta" => sortDesc
                ? query.OrderByDescending(r => r.codruta).ThenByDescending(r => r.codciclo)
                : query.OrderBy(r => r.codruta).ThenBy(r => r.codciclo),
            nameof(RutaListItemDto.Descripcion) or "Descripcion" or "descripcion" => sortDesc
                ? query.OrderByDescending(r => r.descripcion).ThenBy(r => r.codciclo)
                : query.OrderBy(r => r.descripcion).ThenBy(r => r.codciclo),
            nameof(RutaListItemDto.Activo) or "activo" or "estado" => sortDesc
                ? query.OrderByDescending(r => r.estado).ThenBy(r => r.codciclo).ThenBy(r => r.codruta)
                : query.OrderBy(r => r.estado).ThenBy(r => r.codciclo).ThenBy(r => r.codruta),
            _ => query.OrderBy(r => r.codciclo).ThenBy(r => r.codruta)
        };
    }
}
