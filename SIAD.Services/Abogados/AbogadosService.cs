using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Abogados;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Abogados;

public sealed class AbogadosService : IAbogadosService
{
    private readonly SiadDbContext _context;

    public AbogadosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AbogadoListItemDto>> GetAsync(AbogadoFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new AbogadoFilterDto();

        var query = _context.abogados.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(a => a.estado == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(a =>
                    EF.Functions.ILike(a.abogado_codigo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(a.abogado_nombrecorto ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(a.abogado_nombrelargo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(a.abogado_telefono ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(a =>
                    (a.abogado_codigo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (a.abogado_nombrecorto ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (a.abogado_nombrelargo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (a.abogado_telefono ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return await query
            .OrderBy(a => a.abogado_nombrecorto)
            .ThenBy(a => a.abogado_codigo)
            .Select(a => new AbogadoListItemDto
            {
                Id = a.abogado_id,
                Codigo = a.abogado_codigo ?? string.Empty,
                NombreCorto = a.abogado_nombrecorto ?? string.Empty,
                NombreLargo = a.abogado_nombrelargo,
                Telefono = a.abogado_telefono,
                Activo = a.estado,
                CodCuenta = a.codcuenta,
                FechaCreacion = a.fechacreacion,
                UsuarioCreacion = a.usuariocreacion,
                FechaModificacion = a.fechamodificacion,
                UsuarioModificacion = a.usuariomodificacion
            })
            .ToListAsync(ct);
    }

    public async Task<AbogadoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.abogados
            .AsNoTracking()
            .Where(a => a.abogado_id == id)
            .Select(a => new AbogadoEditDto
            {
                Id = a.abogado_id,
                Codigo = a.abogado_codigo ?? string.Empty,
                NombreCorto = a.abogado_nombrecorto ?? string.Empty,
                NombreLargo = a.abogado_nombrelargo,
                Telefono = a.abogado_telefono,
                CodCuenta = a.codcuenta,
                Activo = a.estado
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<AbogadoEditDto> CreateAsync(AbogadoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeRequired(dto.Codigo, 50, "código", uppercase: true);
        var nombreCorto = NormalizeRequired(dto.NombreCorto, 100, "nombre corto");
        var nombreLargo = NormalizeOptional(dto.NombreLargo, 300);
        var telefono = NormalizeOptional(dto.Telefono, 11);
        var codCuenta = NormalizeOptional(dto.CodCuenta, 100, uppercase: true);

        var exists = await _context.abogados
            .AsNoTracking()
            .AnyAsync(a => a.abogado_codigo == codigo, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un abogado con el código {codigo}.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var entity = new abogado
        {
            abogado_codigo = codigo,
            abogado_nombrecorto = nombreCorto,
            abogado_nombrelargo = nombreLargo,
            abogado_telefono = telefono,
            codcuenta = codCuenta,
            estado = dto.Activo,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.abogados.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.abogado_id;
        dto.Codigo = codigo;
        dto.NombreCorto = nombreCorto;
        dto.NombreLargo = nombreLargo;
        dto.Telefono = telefono;
        dto.CodCuenta = codCuenta;
        return dto;
    }

    public async Task<AbogadoEditDto> UpdateAsync(int id, AbogadoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El abogado no es válido.");
        }

        var entity = await _context.abogados.FirstOrDefaultAsync(a => a.abogado_id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("El abogado no existe.");
        }

        var codigo = NormalizeRequired(dto.Codigo, 50, "código", uppercase: true);
        var nombreCorto = NormalizeRequired(dto.NombreCorto, 100, "nombre corto");
        var nombreLargo = NormalizeOptional(dto.NombreLargo, 300);
        var telefono = NormalizeOptional(dto.Telefono, 11);
        var codCuenta = NormalizeOptional(dto.CodCuenta, 100, uppercase: true);

        var exists = await _context.abogados
            .AsNoTracking()
            .AnyAsync(a => a.abogado_codigo == codigo && a.abogado_id != id, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un abogado con el código {codigo}.");
        }

        entity.abogado_codigo = codigo;
        entity.abogado_nombrecorto = nombreCorto;
        entity.abogado_nombrelargo = nombreLargo;
        entity.abogado_telefono = telefono;
        entity.codcuenta = codCuenta;
        entity.estado = dto.Activo;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.abogado_id;
        dto.Codigo = codigo;
        dto.NombreCorto = nombreCorto;
        dto.NombreLargo = nombreLargo;
        dto.Telefono = telefono;
        dto.CodCuenta = codCuenta;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El abogado no es válido.");
        }

        var entity = await _context.abogados.FirstOrDefaultAsync(a => a.abogado_id == id, ct);
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

    private static string NormalizeRequired(string value, int maxLength, string fieldName, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} supera {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? trimmed.ToUpperInvariant() : trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El valor supera {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? trimmed.ToUpperInvariant() : trimmed;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }
}
