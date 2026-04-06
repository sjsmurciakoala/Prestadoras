using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Servicios;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Servicios;

public sealed class ServiciosService : IServiciosService
{
    private readonly SiadDbContext _context;

    public ServiciosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ServicioListItemDto>> GetAsync(ServicioFilterDto? filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .OrderBy(s => s.servicios_descripcioncorta)
            .ThenBy(s => s.servicios_codigo)
            .Select(s => new ServicioListItemDto
            {
                Id = s.servicios_id,
                Codigo = s.servicios_codigo ?? string.Empty,
                DescripcionCorta = s.servicios_descripcioncorta ?? string.Empty,
                DescripcionLarga = s.servicios_descripcionlarga,
                Activo = s.estado,
                FacturableApp = s.facturable_app,
                AppOrden = s.app_orden,
                AppGrupo = s.app_grupo,
                CuentaContableId = s.cont_account_id,
                CuentaContableCodigo = s.cont_account != null ? s.cont_account.code : null,
                CuentaContableNombre = s.cont_account != null ? s.cont_account.name : null
            })
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ServicioListItemDto>> GetPagedAsync(
        ServicioFilterDto? filtro,
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
            .Select(s => new ServicioListItemDto
            {
                Id = s.servicios_id,
                Codigo = s.servicios_codigo ?? string.Empty,
                DescripcionCorta = s.servicios_descripcioncorta ?? string.Empty,
                DescripcionLarga = s.servicios_descripcionlarga,
                Activo = s.estado,
                FacturableApp = s.facturable_app,
                AppOrden = s.app_orden,
                AppGrupo = s.app_grupo,
                CuentaContableId = s.cont_account_id,
                CuentaContableCodigo = s.cont_account != null ? s.cont_account.code : null,
                CuentaContableNombre = s.cont_account != null ? s.cont_account.name : null
            })
            .ToListAsync(ct);

        return new PagedResult<ServicioListItemDto>(items, total);
    }

    public async Task<ServicioEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.servicios
            .AsNoTracking()
            .Where(s => s.servicios_id == id)
            .Select(s => new ServicioEditDto
            {
                Id = s.servicios_id,
                Codigo = s.servicios_codigo ?? string.Empty,
                DescripcionCorta = s.servicios_descripcioncorta ?? string.Empty,
                DescripcionLarga = s.servicios_descripcionlarga,
                Activo = s.estado,
                FacturableApp = s.facturable_app,
                AppOrden = s.app_orden,
                AppGrupo = s.app_grupo,
                CuentaContableId = s.cont_account_id
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ServicioEditDto> CreateAsync(ServicioEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeRequired(dto.Codigo, 50, "codigo", uppercase: true);
        var descripcionCorta = NormalizeRequired(dto.DescripcionCorta, 100, "descripcion corta");
        var descripcionLarga = NormalizeOptional(dto.DescripcionLarga, 300);

        var exists = await _context.servicios
            .AsNoTracking()
            .AnyAsync(s => s.servicios_codigo == codigo, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un servicio con el codigo {codigo}.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var contAccountId = await ValidateCuentaContableAsync(dto.CuentaContableId, ct);

        var entity = new servicio
        {
            servicios_codigo = codigo,
            servicios_descripcioncorta = descripcionCorta,
            servicios_descripcionlarga = descripcionLarga,
            estado = dto.Activo,
            facturable_app = dto.FacturableApp,
            app_orden = dto.AppOrden,
            app_grupo = NormalizeOptional(dto.AppGrupo, 20),
            cont_account_id = contAccountId,
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.servicios.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.servicios_id;
        dto.Codigo = codigo;
        dto.DescripcionCorta = descripcionCorta;
        dto.DescripcionLarga = descripcionLarga;
        dto.FacturableApp = entity.facturable_app;
        dto.AppOrden = entity.app_orden;
        dto.AppGrupo = entity.app_grupo;
        dto.CuentaContableId = entity.cont_account_id;
        return dto;
    }

    public async Task<ServicioEditDto> UpdateAsync(int id, ServicioEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El servicio no es valido.");
        }

        var entity = await _context.servicios.FirstOrDefaultAsync(s => s.servicios_id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("El servicio no existe.");
        }

        var codigo = NormalizeRequired(dto.Codigo, 50, "codigo", uppercase: true);
        var descripcionCorta = NormalizeRequired(dto.DescripcionCorta, 100, "descripcion corta");
        var descripcionLarga = NormalizeOptional(dto.DescripcionLarga, 300);

        var exists = await _context.servicios
            .AsNoTracking()
            .AnyAsync(s => s.servicios_codigo == codigo && s.servicios_id != id, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un servicio con el codigo {codigo}.");
        }

        var contAccountId = await ValidateCuentaContableAsync(dto.CuentaContableId, ct);

        entity.servicios_codigo = codigo;
        entity.servicios_descripcioncorta = descripcionCorta;
        entity.servicios_descripcionlarga = descripcionLarga;
        entity.estado = dto.Activo;
        entity.facturable_app = dto.FacturableApp;
        entity.app_orden = dto.AppOrden;
        entity.app_grupo = NormalizeOptional(dto.AppGrupo, 20);
        entity.cont_account_id = contAccountId;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.servicios_id;
        dto.Codigo = codigo;
        dto.DescripcionCorta = descripcionCorta;
        dto.DescripcionLarga = descripcionLarga;
        dto.FacturableApp = entity.facturable_app;
        dto.AppOrden = entity.app_orden;
        dto.AppGrupo = entity.app_grupo;
        dto.CuentaContableId = entity.cont_account_id;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El servicio no es valido.");
        }

        var entity = await _context.servicios.FirstOrDefaultAsync(s => s.servicios_id == id, ct);
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

    private IQueryable<servicio> BuildQuery(ServicioFilterDto? filtro)
    {
        filtro ??= new ServicioFilterDto();

        var query = _context.servicios.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(s => s.estado == filtro.Activo.Value);
        }

        if (filtro.FacturableApp.HasValue)
        {
            query = query.Where(s => s.facturable_app == filtro.FacturableApp.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(s =>
                    EF.Functions.ILike(s.servicios_codigo ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(s.servicios_descripcioncorta ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(s.servicios_descripcionlarga ?? string.Empty, likePattern) ||
                    EF.Functions.ILike(s.app_grupo ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(s =>
                    (s.servicios_codigo ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (s.servicios_descripcioncorta ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (s.servicios_descripcionlarga ?? string.Empty).ToLowerInvariant().Contains(lowered) ||
                    (s.app_grupo ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return query;
    }

    private static IQueryable<servicio> ApplySort(IQueryable<servicio> query, string? sortField, bool sortDesc)
    {
        var field = sortField?.Trim();
        if (string.IsNullOrWhiteSpace(field))
        {
            return query.OrderBy(s => s.servicios_descripcioncorta).ThenBy(s => s.servicios_codigo);
        }

        return field.ToLowerInvariant() switch
        {
            "codigo" => sortDesc ? query.OrderByDescending(s => s.servicios_codigo) : query.OrderBy(s => s.servicios_codigo),
            "descripcioncorta" => sortDesc ? query.OrderByDescending(s => s.servicios_descripcioncorta) : query.OrderBy(s => s.servicios_descripcioncorta),
            "descripcionlarga" => sortDesc ? query.OrderByDescending(s => s.servicios_descripcionlarga) : query.OrderBy(s => s.servicios_descripcionlarga),
            "activo" => sortDesc ? query.OrderByDescending(s => s.estado) : query.OrderBy(s => s.estado),
            "facturableapp" => sortDesc ? query.OrderByDescending(s => s.facturable_app) : query.OrderBy(s => s.facturable_app),
            "apporden" => sortDesc ? query.OrderByDescending(s => s.app_orden) : query.OrderBy(s => s.app_orden),
            "appgrupo" => sortDesc ? query.OrderByDescending(s => s.app_grupo) : query.OrderBy(s => s.app_grupo),
            _ => query.OrderBy(s => s.servicios_descripcioncorta).ThenBy(s => s.servicios_codigo)
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

    private static string? NormalizeOptional(string? value, int maxLength, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El valor no puede superar {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }

    private async Task<long?> ValidateCuentaContableAsync(long? accountId, CancellationToken ct)
    {
        if (!accountId.HasValue || accountId.Value <= 0)
        {
            return null;
        }

        var cuenta = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.account_id == accountId.Value)
            .Select(c => new { c.account_id, c.allows_posting, c.status })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null)
        {
            throw new InvalidOperationException("La cuenta contable no existe en la empresa actual.");
        }

        if (!cuenta.allows_posting)
        {
            throw new InvalidOperationException("La cuenta contable no permite movimientos.");
        }

        if (!IsCuentaActiva(cuenta.status))
        {
            throw new InvalidOperationException("La cuenta contable esta inactiva.");
        }

        return cuenta.account_id;
    }

    private static bool IsCuentaActiva(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return true;
        }

        return string.Equals(status, "ACTIVE", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(status, "ACTIVO", StringComparison.OrdinalIgnoreCase);
    }
}
