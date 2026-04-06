using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Letras;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Letras;

public class LetrasService : ILetrasService
{
    private readonly SiadDbContext _context;

    public LetrasService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LetraListItemDto>> GetLetrasAsync(LetraFilterDto filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(l => l.num)
            .ThenBy(l => l.letras)
            .Select(l => new LetraListItemDto { Letra = l.letras, Numero = l.num })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<LetraListItemDto>> GetLetrasPagedAsync(
        LetraFilterDto filtro,
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
            skip = 0;

        if (take <= 0)
            take = 50;

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(l => new LetraListItemDto { Letra = l.letras, Numero = l.num })
            .ToListAsync(ct);

        return new PagedResult<LetraListItemDto>(items, totalCount);
    }

    public async Task<LetraDetailDto?> GetLetraAsync(string letra, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(letra))
            return null;

        var normalized = NormalizeLetra(letra);

        return await _context.letras
            .AsNoTracking()
            .Where(l => l.letras == normalized)
            .Select(l => new LetraDetailDto
            {
                Letra = l.letras,
                Numero = l.num,
                FechaCreacion = l.fechacreacion,
                UsuarioCreacion = l.usuariocreacion,
                FechaModificacion = l.fechamodificacion,
                UsuarioModificacion = l.usuariomodificacion
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task CreateLetraAsync(LetraEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.Letra);

        var normalized = NormalizeLetra(dto.Letra);

        // Validar que no exista
        if (await _context.letras.AnyAsync(l => l.letras == normalized, ct))
            throw new InvalidOperationException($"La letra '{normalized}' ya existe.");

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var entity = new letra
        {
            letras = normalized,
            num = dto.Numero,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.letras.Add(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateLetraAsync(string letra, LetraEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(letra);

        var normalized = NormalizeLetra(letra);

        var entity = await _context.letras
            .FirstOrDefaultAsync(l => l.letras == normalized, ct);

        if (entity == null)
            throw new KeyNotFoundException($"Letra '{normalized}' no encontrada.");

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // No permitir cambiar el código de letra
        entity.num = dto.Numero;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = now;

        _context.letras.Update(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteLetraAsync(string letra, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(letra))
            return false;

        var normalized = NormalizeLetra(letra);

        var entity = await _context.letras
            .FirstOrDefaultAsync(l => l.letras == normalized, ct);

        if (entity == null)
            return false;

        _context.letras.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private IQueryable<letra> BuildQuery(LetraFilterDto filtro)
    {
        var query = _context.letras.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro?.Search))
        {
            var search = filtro.Search.Trim().ToUpperInvariant();
            try
            {
                query = query.Where(l => EF.Functions.ILike(l.letras, $"%{search}%"));
            }
            catch
            {
                // Fallback para proveedores in-memory
                query = query.Where(l => l.letras.ToUpper().Contains(search));
            }
        }

        return query;
    }

    private IQueryable<letra> ApplySort(IQueryable<letra> query, string? sortField, bool sortDesc)
    {
        if (string.IsNullOrWhiteSpace(sortField))
            return query.OrderBy(l => l.num).ThenBy(l => l.letras);

        return sortField switch
        {
            "letra" => sortDesc ? query.OrderByDescending(l => l.letras) : query.OrderBy(l => l.letras),
            "numero" => sortDesc ? query.OrderByDescending(l => l.num) : query.OrderBy(l => l.num),
            _ => query.OrderBy(l => l.num).ThenBy(l => l.letras)
        };
    }

    private static string NormalizeLetra(string value)
    {
        return value?.Trim().ToUpperInvariant() ?? string.Empty;
    }

    private static string? NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "System" : user.Trim();
    }
}
