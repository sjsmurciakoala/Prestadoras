using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Conceptos;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Conceptos;

public sealed class ConceptosService : IConceptosService
{
    private readonly SiadDbContext _context;

    public ConceptosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ConceptoListItemDto>> GetAsync(ConceptoFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(c => c.descripcion)
            .ThenBy(c => c.depto)
            .ThenBy(c => c.tipo)
            .Select(c => new ConceptoListItemDto
            {
                Id = c.tipo_id,
                Depto = c.depto ?? string.Empty,
                Tipo = c.tipo ?? string.Empty,
                Descripcion = c.descripcion ?? string.Empty,
                Concepto = c.concepto ?? string.Empty,
                DeptoAppMiTrabajo = c.depto_appmitrabajo ?? string.Empty,
                Activo = c.estado
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ConceptoListItemDto>> GetPagedAsync(
        ConceptoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        if (take <= 0)
        {
            take = 50;
        }

        if (take > 500)
        {
            take = 500;
        }

        if (skip < 0)
        {
            skip = 0;
        }

        var query = BuildQuery(filtro);
        query = ApplySort(query, sortField, sortDesc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(c => new ConceptoListItemDto
            {
                Id = c.tipo_id,
                Depto = c.depto ?? string.Empty,
                Tipo = c.tipo ?? string.Empty,
                Descripcion = c.descripcion ?? string.Empty,
                Concepto = c.concepto ?? string.Empty,
                DeptoAppMiTrabajo = c.depto_appmitrabajo ?? string.Empty,
                Activo = c.estado
            })
            .ToListAsync(ct);

        return new PagedResult<ConceptoListItemDto>(items, total);
    }

    public async Task<ConceptoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.tipo_ds
            .AsNoTracking()
            .Where(c => c.tipo_id == id)
            .Select(c => new ConceptoEditDto
            {
                Id = c.tipo_id,
                Depto = c.depto ?? string.Empty,
                Tipo = c.tipo ?? string.Empty,
                Descripcion = c.descripcion ?? string.Empty,
                Concepto = c.concepto ?? string.Empty,
                DeptoAppMiTrabajo = c.depto_appmitrabajo ?? string.Empty,
                Activo = c.estado
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ConceptoEditDto> CreateAsync(ConceptoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var depto = NormalizeRequired(dto.Depto, 2, "depto", uppercase: true);
        var tipo = NormalizeRequired(dto.Tipo, 2, "tipo", uppercase: true);
        var descripcion = NormalizeRequired(dto.Descripcion, 80, "descripcion");
        var concepto = NormalizeRequired(dto.Concepto, 200, "concepto");
        var deptoApp = NormalizeRequired(dto.DeptoAppMiTrabajo, 2, "depto app mi trabajo", uppercase: true);

        var entity = new tipo_d
        {
            depto = depto,
            tipo = tipo,
            descripcion = descripcion,
            concepto = concepto,
            depto_appmitrabajo = deptoApp,
            estado = dto.Activo,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.tipo_ds.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.tipo_id;
        dto.Depto = depto;
        dto.Tipo = tipo;
        dto.Descripcion = descripcion;
        dto.Concepto = concepto;
        dto.DeptoAppMiTrabajo = deptoApp;
        return dto;
    }

    public async Task<ConceptoEditDto> UpdateAsync(int id, ConceptoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El concepto no es valido.");
        }

        var entity = await _context.tipo_ds.FirstOrDefaultAsync(c => c.tipo_id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("El concepto no existe.");
        }

        var depto = NormalizeRequired(dto.Depto, 2, "depto", uppercase: true);
        var tipo = NormalizeRequired(dto.Tipo, 2, "tipo", uppercase: true);
        var descripcion = NormalizeRequired(dto.Descripcion, 80, "descripcion");
        var concepto = NormalizeRequired(dto.Concepto, 200, "concepto");
        var deptoApp = NormalizeRequired(dto.DeptoAppMiTrabajo, 2, "depto app mi trabajo", uppercase: true);

        entity.depto = depto;
        entity.tipo = tipo;
        entity.descripcion = descripcion;
        entity.concepto = concepto;
        entity.depto_appmitrabajo = deptoApp;
        entity.estado = dto.Activo;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.tipo_id;
        dto.Depto = depto;
        dto.Tipo = tipo;
        dto.Descripcion = descripcion;
        dto.Concepto = concepto;
        dto.DeptoAppMiTrabajo = deptoApp;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El concepto no es valido.");
        }

        var entity = await _context.tipo_ds.FirstOrDefaultAsync(c => c.tipo_id == id, ct);
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

    private IQueryable<tipo_d> BuildQuery(ConceptoFilterDto? filtro)
    {
        filtro ??= new ConceptoFilterDto();

        var query = _context.tipo_ds.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(c => c.estado == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(c =>
                    EF.Functions.ILike(c.depto ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.tipo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.descripcion ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.concepto ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(c.depto_appmitrabajo ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(c =>
                    (c.depto ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.tipo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.descripcion ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.concepto ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (c.depto_appmitrabajo ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return query;
    }

    private static IQueryable<tipo_d> ApplySort(IQueryable<tipo_d> query, string? sortField, bool sortDesc)
    {
        var field = sortField?.Trim();
        if (string.IsNullOrWhiteSpace(field))
        {
            return query.OrderBy(c => c.descripcion).ThenBy(c => c.depto).ThenBy(c => c.tipo);
        }

        return field.ToLowerInvariant() switch
        {
            "depto" => sortDesc ? query.OrderByDescending(c => c.depto) : query.OrderBy(c => c.depto),
            "tipo" => sortDesc ? query.OrderByDescending(c => c.tipo) : query.OrderBy(c => c.tipo),
            "descripcion" => sortDesc ? query.OrderByDescending(c => c.descripcion) : query.OrderBy(c => c.descripcion),
            "concepto" => sortDesc ? query.OrderByDescending(c => c.concepto) : query.OrderBy(c => c.concepto),
            "deptoappmitrabajo" => sortDesc ? query.OrderByDescending(c => c.depto_appmitrabajo) : query.OrderBy(c => c.depto_appmitrabajo),
            "activo" => sortDesc ? query.OrderByDescending(c => c.estado) : query.OrderBy(c => c.estado),
            _ => query.OrderBy(c => c.descripcion).ThenBy(c => c.depto).ThenBy(c => c.tipo)
        };
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName, bool uppercase = false)
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

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }
}
