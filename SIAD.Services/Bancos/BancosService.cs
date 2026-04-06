using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class BancosService : IBancosService
{
    private readonly SiadDbContext context;
    private readonly ICurrentCompanyService currentCompanyService;

    public BancosService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        this.context = context;
        this.currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<BancoListDto>> GetAsync(BancoFilterDto? filtro, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var query = context.ban_banco
            .AsNoTracking()
            .Where(b => b.company_id == companyId);

        if (!string.IsNullOrWhiteSpace(filtro?.Nombre))
        {
            var term = filtro.Nombre.Trim();
            var likePattern = $"%{term}%";
            if (context.Database.IsRelational())
            {
                query = query.Where(b => EF.Functions.ILike(b.nombre, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(b => b.nombre.ToLowerInvariant().Contains(lowered));
            }
        }

        if (filtro?.Activo.HasValue == true)
        {
            query = query.Where(b => b.activo == filtro.Activo.Value);
        }

        return await query
            .OrderBy(b => b.nombre)
            .ThenBy(b => b.ban_banco_id)
            .Select(b => new BancoListDto
            {
                BancoId = b.ban_banco_id,
                CompanyId = b.company_id,
                Nombre = b.nombre,
                Activo = b.activo,
                CreatedAt = b.created_at,
                CreatedBy = b.created_by,
                UpdatedAt = b.updated_at,
                UpdatedBy = b.updated_by
            })
            .ToListAsync(ct);
    }

    public async Task<BancoEditDto?> GetByIdAsync(long bancoId, CancellationToken ct = default)
    {
        if (bancoId <= 0)
        {
            return null;
        }

        var companyId = EnsureCompanyId();
        return await context.ban_banco
            .AsNoTracking()
            .Where(b => b.company_id == companyId && b.ban_banco_id == bancoId)
            .Select(b => MapToEditDto(b))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<BancoEditDto> CreateAsync(BancoCreateDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var companyId = EnsureCompanyId();
        var codigo = await GenerateCodigoAsync(companyId, ct);
        var nombre = NormalizeRequired(dto.Nombre, 60, "nombre");

        var exists = await context.ban_banco
            .AsNoTracking()
            .AnyAsync(b => b.company_id == companyId && b.code == codigo, ct);
        if (exists)
        {
            throw new InvalidOperationException("No fue posible generar el codigo del banco.");
        }

        var entity = new ban_banco
        {
            company_id = companyId,
            code = codigo,
            nombre = nombre,
            activo = dto.Activo,
            created_at = DateTime.UtcNow,
            created_by = NormalizeUser(user)
        };

        context.ban_banco.Add(entity);
        await context.SaveChangesAsync(ct);
        return MapToEditDto(entity);
    }

    public async Task<BancoEditDto> UpdateAsync(long bancoId, BancoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (bancoId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bancoId), "El banco no es valido.");
        }

        var companyId = EnsureCompanyId();
        var entity = await context.ban_banco
            .FirstOrDefaultAsync(b => b.company_id == companyId && b.ban_banco_id == bancoId, ct);

        if (entity is null)
        {
            throw new KeyNotFoundException("El banco no existe.");
        }

        entity.nombre = NormalizeRequired(dto.Nombre, 60, "nombre");
        entity.activo = dto.Activo;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = NormalizeUser(user);

        await context.SaveChangesAsync(ct);
        return MapToEditDto(entity);
    }

    public async Task<bool> DeleteAsync(long bancoId, CancellationToken ct = default)
    {
        if (bancoId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(bancoId), "El banco no es valido.");
        }

        var companyId = EnsureCompanyId();
        var entity = await context.ban_banco
            .FirstOrDefaultAsync(b => b.company_id == companyId && b.ban_banco_id == bancoId, ct);

        if (entity is null)
        {
            return false;
        }

        var cuentasQuery = context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.ban_banco_id == bancoId);

        var tieneCuentasActivas = await cuentasQuery.AnyAsync(c => c.activo, ct);
        if (tieneCuentasActivas)
        {
            throw new InvalidOperationException("El banco tiene cuentas bancarias activas y no puede eliminarse.");
        }

        var tieneCuentas = await cuentasQuery.AnyAsync(ct);
        if (tieneCuentas)
        {
            throw new InvalidOperationException("El banco tiene cuentas bancarias asociadas y no puede eliminarse.");
        }

        context.ban_banco.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    private long EnsureCompanyId()
    {
        var companyId = currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual.");
        }

        return companyId;
    }

    private static BancoEditDto MapToEditDto(ban_banco entity)
    {
        return new BancoEditDto
        {
            BancoId = entity.ban_banco_id,
            CompanyId = entity.company_id,
            Nombre = entity.nombre,
            Activo = entity.activo,
            CreatedAt = entity.created_at,
            CreatedBy = entity.created_by,
            UpdatedAt = entity.updated_at,
            UpdatedBy = entity.updated_by
        };
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

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }

    private async Task<string> GenerateCodigoAsync(long companyId, CancellationToken ct)
    {
        var codes = await context.ban_banco
            .AsNoTracking()
            .Where(b => b.company_id == companyId)
            .Select(b => b.code)
            .ToListAsync(ct);

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long maxNumeric = 0;

        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            var trimmed = code.Trim();
            used.Add(trimmed);

            if (long.TryParse(trimmed, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                if (value > maxNumeric)
                {
                    maxNumeric = value;
                }
            }
        }

        var next = Math.Max(1, maxNumeric + 1);
        var candidate = next.ToString(CultureInfo.InvariantCulture);
        while (used.Contains(candidate))
        {
            next++;
            candidate = next.ToString(CultureInfo.InvariantCulture);
        }

        return candidate;
    }
}
