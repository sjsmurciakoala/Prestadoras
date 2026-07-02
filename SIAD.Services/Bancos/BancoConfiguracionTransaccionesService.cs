using Microsoft.EntityFrameworkCore;
using System.Globalization;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Bancos;

public sealed class BancoConfiguracionTransaccionesService : IBancoConfiguracionTransaccionesService
{
    private readonly SiadDbContext context;
    private readonly ICurrentCompanyService currentCompanyService;

    public BancoConfiguracionTransaccionesService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService)
    {
        this.context = context;
        this.currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<BancoConfiguracionTransaccionListDto>> GetAsync(
        BancoConfiguracionTransaccionFilterDto? filtro,
        CancellationToken ct = default)
    {
        filtro ??= new BancoConfiguracionTransaccionFilterDto();

        var companyId = EnsureCompanyId();

        var query = context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.EntraSale))
        {
            var entraSale = filtro.EntraSale.Trim().ToUpperInvariant();
            query = query.Where(q => q.entra_sale == entraSale);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var pattern = $"%{term}%";

            if (context.Database.IsRelational())
            {
                query = query.Where(q =>
                    EF.Functions.ILike(q.tipo_transaccion, pattern) ||
                    EF.Functions.ILike(q.nombre, pattern) ||
                    EF.Functions.ILike(q.correlativo, pattern) ||
                    (q.cuenta_contable != null && EF.Functions.ILike(q.cuenta_contable, pattern)));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(q =>
                    q.tipo_transaccion.ToLower().Contains(lowered) ||
                    q.nombre.ToLower().Contains(lowered) ||
                    q.correlativo.ToLower().Contains(lowered) ||
                    (q.cuenta_contable != null && q.cuenta_contable.ToLower().Contains(lowered)));
            }
        }

        var raw = await
            (from cfg in query
             join centro in context.con_centro_costos.AsNoTracking()
                 on cfg.cod_centrocosto equals (long?)centro.cost_center_id into centroJoin
             from centro in centroJoin.DefaultIfEmpty()
             orderby cfg.tipo_transaccion
             select new { cfg, centro })
            .ToListAsync(ct);

        return raw.Select(item => new BancoConfiguracionTransaccionListDto
        {
            TipoTransaccionId = NormalizeText(item.cfg.tipo_transaccion),
            DescripcionTransaccion = NormalizeText(item.cfg.nombre),
            Correlativo = NormalizeText(item.cfg.correlativo),
            EntraSale = NormalizeText(item.cfg.entra_sale),
            UsaCentroCosto = item.cfg.cod_centrocosto.HasValue && item.cfg.cod_centrocosto.Value > 0,
            CentroCostoId = item.cfg.cod_centrocosto,
            CentroCostoCodigo = item.centro != null ? item.centro.code : null,
            CentroCostoNombre = item.centro != null ? item.centro.name : null,
            CuentaContable = item.cfg.cuenta_contable,
            TipoPartida = NormalizeOptional(item.cfg.cod_tipopartida),
            EmiteCheque = FlagToBool(item.cfg.emite_cheque),
            DelSistema = FlagToBool(item.cfg.del_sistema),
            Pad = FlagToBool(item.cfg.pad),
            Pda = FlagToBool(item.cfg.pda),
            DetalleContable = false,
            CuentaAlterna = item.cfg.cuenta_alterna
        }).ToList();
    }

    public async Task<BancoConfiguracionTransaccionEditDto?> GetByIdAsync(
        string tipoTransaccionId,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipoTransaccionId))
        {
            return null;
        }

        var companyId = EnsureCompanyId();
        var key = tipoTransaccionId.Trim();

        var entity = await context.ban_tipos_transacciones
            .AsNoTracking()
            .Where(t => t.company_id == companyId && t.tipo_transaccion == key)
            .OrderBy(t => t.ban_tipo_transaccion_id)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : MapToEditDto(entity);
    }

    public async Task<BancoConfiguracionTransaccionEditDto> CreateAsync(
        BancoConfiguracionTransaccionEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var companyId = EnsureCompanyId();
        var key = NormalizeRequired(dto.TipoTransaccionId, 3, nameof(dto.TipoTransaccionId));

        var exists = await context.ban_tipos_transacciones
            .AsNoTracking()
            .AnyAsync(t => t.company_id == companyId && t.tipo_transaccion == key, ct);

        if (exists)
        {
            throw new InvalidOperationException("Ya existe una configuracion con el codigo indicado.");
        }

        var entity = new ban_tipos_transacciones
        {
            company_id = companyId,
            tipo_transaccion = key,
            created_at = DateTime.UtcNow,
            created_by = NormalizeUser(user),
            updated_at = DateTime.UtcNow,
            updated_by = NormalizeUser(user)
        };

        MapToEntity(entity, dto);
        await ValidarTipoPartidaAsync(dto.TipoPartidaTypeId, companyId, ct);
        await ValidarCentroCostoAsync(dto.UsaCentroCosto, entity.cod_centrocosto, ct);
        EnsureEstado(entity);

        context.ban_tipos_transacciones.Add(entity);
        await context.SaveChangesAsync(ct);

        return MapToEditDto(entity);
    }

    public async Task<BancoConfiguracionTransaccionEditDto> UpdateAsync(
        string tipoTransaccionId,
        BancoConfiguracionTransaccionEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(tipoTransaccionId))
        {
            throw new ArgumentException("El codigo es obligatorio.", nameof(tipoTransaccionId));
        }

        var key = tipoTransaccionId.Trim();
        if (!string.IsNullOrWhiteSpace(dto.TipoTransaccionId)
            && !string.Equals(dto.TipoTransaccionId.Trim(), key, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("El codigo solicitado no coincide con el registro.", nameof(dto.TipoTransaccionId));
        }

        var companyId = EnsureCompanyId();
        var entity = await context.ban_tipos_transacciones
            .FirstOrDefaultAsync(t => t.company_id == companyId && t.tipo_transaccion == key, ct)
            ?? throw new KeyNotFoundException("No se encontro la configuracion solicitada.");

        MapToEntity(entity, dto);
        await ValidarTipoPartidaAsync(dto.TipoPartidaTypeId, companyId, ct);
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = NormalizeUser(user);

        await ValidarCentroCostoAsync(dto.UsaCentroCosto, entity.cod_centrocosto, ct);
        await context.SaveChangesAsync(ct);

        return MapToEditDto(entity);
    }

    public async Task<bool> DeleteAsync(string tipoTransaccionId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(tipoTransaccionId))
        {
            return false;
        }

        var companyId = EnsureCompanyId();
        var key = tipoTransaccionId.Trim();
        var entity = await context.ban_tipos_transacciones
            .FirstOrDefaultAsync(t => t.company_id == companyId && t.tipo_transaccion == key, ct);

        if (entity is null)
        {
            return false;
        }

        context.ban_tipos_transacciones.Remove(entity);
        await context.SaveChangesAsync(ct);
        return true;
    }

    private async Task ValidarCentroCostoAsync(bool usaCentroCosto, long? centroCostoId, CancellationToken ct)
    {
        if (!usaCentroCosto)
        {
            return;
        }

        if (!centroCostoId.HasValue || centroCostoId.Value <= 0)
        {
            throw new ArgumentException("Seleccione un centro de costo valido.", nameof(centroCostoId));
        }

        var existe = await context.con_centro_costos
            .AsNoTracking()
            .AnyAsync(c => c.cost_center_id == centroCostoId.Value, ct);

        if (!existe)
        {
            throw new ArgumentException("El centro de costo seleccionado no existe.", nameof(centroCostoId));
        }
    }

    private async Task ValidarTipoPartidaAsync(long? tipoPartidaTypeId, long companyId, CancellationToken ct)
    {
        if (!tipoPartidaTypeId.HasValue || tipoPartidaTypeId.Value <= 0)
        {
            throw new ArgumentException("Seleccione un tipo de partida valido.", nameof(tipoPartidaTypeId));
        }

        var exists = await context.con_tipo_transacciones
            .AsNoTracking()
            .AnyAsync(t => t.company_id == companyId && t.type_id == tipoPartidaTypeId.Value, ct);

        if (!exists)
        {
            throw new ArgumentException("El tipo de partida seleccionado no existe.", nameof(tipoPartidaTypeId));
        }
    }

    private static BancoConfiguracionTransaccionEditDto MapToEditDto(ban_tipos_transacciones entity)
    {
        return new BancoConfiguracionTransaccionEditDto
        {
            TipoTransaccionId = NormalizeText(entity.tipo_transaccion),
            DescripcionTransaccion = NormalizeText(entity.nombre),
            TipoPartidaTypeId = ParseTipoPartidaTypeId(entity.cod_tipopartida),
            UsaCentroCosto = entity.cod_centrocosto.HasValue && entity.cod_centrocosto.Value > 0,
            CentroCostoId = entity.cod_centrocosto,
            Correlativo = NormalizeText(entity.correlativo),
            CuentaContable = entity.cuenta_contable,
            Destino = entity.destino,
            EntraSale = NormalizeText(entity.entra_sale),
            DelSistema = FlagToBool(entity.del_sistema),
            EmiteCheque = FlagToBool(entity.emite_cheque),
            Pad = FlagToBool(entity.pad),
            Pda = FlagToBool(entity.pda),
            DetalleContable = false,
            CuentaAlterna = entity.cuenta_alterna,
            Observaciones = entity.observaciones
        };
    }

    private static void MapToEntity(ban_tipos_transacciones entity, BancoConfiguracionTransaccionEditDto dto)
    {
        entity.tipo_transaccion = NormalizeRequired(dto.TipoTransaccionId, 3, nameof(dto.TipoTransaccionId));
        entity.nombre = NormalizeRequired(dto.DescripcionTransaccion, 40, nameof(dto.DescripcionTransaccion));
        entity.cod_tipopartida = FormatTipoPartidaTypeId(dto.TipoPartidaTypeId);
        entity.cod_centrocosto = dto.UsaCentroCosto ? dto.CentroCostoId : null;
        entity.correlativo = NormalizeRequired(dto.Correlativo, 6, nameof(dto.Correlativo));
        entity.cuenta_contable = NormalizeOptional(dto.CuentaContable, 13, nameof(dto.CuentaContable));
        entity.destino = NormalizeOptional(dto.Destino, 9, nameof(dto.Destino));
        entity.entra_sale = NormalizeRequired(dto.EntraSale, 1, nameof(dto.EntraSale)).ToUpperInvariant();
        entity.del_sistema = ToFlag(dto.DelSistema);
        entity.emite_cheque = ToFlag(dto.EmiteCheque);
        entity.pad = ToFlag(dto.Pad);
        entity.pda = ToFlag(dto.Pda);
        entity.cuenta_alterna = dto.CuentaAlterna;
        entity.observaciones = string.IsNullOrWhiteSpace(dto.Observaciones) ? null : dto.Observaciones.Trim();
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El campo {fieldName} es obligatorio.", fieldName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El campo {fieldName} no puede superar {maxLength} caracteres.", fieldName);
        }

        return trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El campo {fieldName} no puede superar {maxLength} caracteres.", fieldName);
        }

        return trimmed;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
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

    private static bool FlagToBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        return normalized.Equals("S", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("Y", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("1", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("T", StringComparison.OrdinalIgnoreCase)
            || normalized.Equals("TRUE", StringComparison.OrdinalIgnoreCase);
    }

    private static string ToFlag(bool value)
    {
        return value ? "S" : "N";
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static long? ParseTipoPartidaTypeId(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return long.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string FormatTipoPartidaTypeId(long? value)
    {
        if (!value.HasValue || value.Value <= 0)
        {
            throw new ArgumentException("El campo TipoPartidaTypeId es obligatorio.", nameof(value));
        }

        return value.Value.ToString(CultureInfo.InvariantCulture);
    }

    private static void EnsureEstado(ban_tipos_transacciones entity)
    {
        if (string.IsNullOrWhiteSpace(entity.estado))
        {
            entity.estado = "ACTIVE";
        }
    }
}
