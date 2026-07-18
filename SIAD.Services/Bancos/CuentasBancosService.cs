using System.Globalization;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.DTOs.Bancos;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Contabilidad;

namespace SIAD.Services.Bancos;

public sealed class CuentasBancosService : ICuentasBancosService
{
    private const int TitularMaxLength = 150;
    private const int ObservacionesMaxLength = 150;

    private readonly SiadDbContext context;
    private readonly ICurrentCompanyService currentCompanyService;
    private readonly IAccountFormatService accountFormatService;

    public CuentasBancosService(
        SiadDbContext context,
        ICurrentCompanyService currentCompanyService,
        IAccountFormatService accountFormatService)
    {
        this.context = context;
        this.currentCompanyService = currentCompanyService;
        this.accountFormatService = accountFormatService;
    }

    public async Task<IReadOnlyList<BancoCuentaListDto>> GetAsync(long companyId, CancellationToken ct = default)
    {
        var currentCompanyId = EnsureCompanyId();
        if (companyId <= 0 || companyId != currentCompanyId)
        {
            throw new InvalidOperationException("La empresa solicitada no es válida para el usuario actual.");
        }

        var planQuery = context.con_plan_cuentas
            .AsNoTracking()
            .Where(p => p.company_id == companyId);

        var resultado = await
            (from cuenta in context.ban_cuenta
                 .AsNoTracking()
                 .Include(c => c.ban_banco)
             where cuenta.company_id == companyId
             join plan in planQuery on cuenta.cont_account_id equals (long?)plan.account_id into planJoin
             from plan in planJoin.DefaultIfEmpty()
             orderby cuenta.nombre, cuenta.banco_cuenta_id
             select new BancoCuentaListDto
             {
                 BancoCuentaId = cuenta.banco_cuenta_id,
                 CompanyId = cuenta.company_id,
                 BancoId = cuenta.ban_banco_id ?? 0,
                 BancoNombre = cuenta.ban_banco != null ? cuenta.ban_banco.nombre : null,
                 NumeroCuenta = cuenta.numero_cuenta,
                 TipoCuenta = cuenta.tipo,
                 Moneda = cuenta.currency_code,
                 SaldoActual = cuenta.saldo_actual,
                 Titular = cuenta.nombre,
                 Observaciones = cuenta.banco_nombre,
                 Activo = cuenta.activo,
                 CreatedAt = cuenta.created_at,
                 CtaConc = cuenta.cta_conc.HasValue ? cuenta.cta_conc.Value.ToString() : null,
                 CuentaContableCodigo = plan.code,
                 CuentaContableNombre = plan.name
             }).ToListAsync(ct);

        foreach (var item in resultado)
        {
            if (string.IsNullOrWhiteSpace(item.CtaConc))
            {
                item.CtaConc = "0";
            }
        }

        return resultado;
    }

    public async Task<IReadOnlyList<BancoCuentaConciliacionDto>> GetConciliacionAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly fechaHasta,
        CancellationToken ct = default)
    {
        var currentCompanyId = EnsureCompanyId();
        if (companyId <= 0 || companyId != currentCompanyId)
        {
            throw new InvalidOperationException("La empresa solicitada no es valida para el usuario actual.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);

        var fechaDesde = new DateOnly(fechaHasta.Year, fechaHasta.Month, 1);


        var sql = @"
SELECT
    company_id AS ""CompanyId"",
    banco_cuenta_id AS ""BancoCuentaId"",
    numero_transaccion AS ""NumeroTransaccion"",
    fecha AS ""Fecha"",
    tipo AS ""Tipo"",
    referencia AS ""Referencia"",
    monto AS ""Monto"",
    estado_conciliacion AS ""EstadoConciliacion""
FROM public.conciliacion_cuenta_bancos
WHERE company_id = {0}
  AND banco_cuenta_id = {1}
  AND (
        (fecha >= {2} AND fecha <= {3})
        OR (
            fecha < {2}
            AND UPPER(COALESCE(estado_conciliacion, 'NOC')) <> 'CON'
        )
  )
ORDER BY fecha, numero_transaccion";
        var sqlSinReferencia = @"
SELECT
    company_id AS ""CompanyId"",
    banco_cuenta_id AS ""BancoCuentaId"",
    numero_transaccion AS ""NumeroTransaccion"",
    fecha AS ""Fecha"",
    tipo AS ""Tipo"",
    NULL AS ""Referencia"",
    monto AS ""Monto"",
    estado_conciliacion AS ""EstadoConciliacion""
FROM public.conciliacion_cuenta_bancos
WHERE company_id = {0}
  AND banco_cuenta_id = {1}
  AND (
        (fecha >= {2} AND fecha <= {3})
        OR (
            fecha < {2}
            AND UPPER(COALESCE(estado_conciliacion, 'NOC')) <> 'CON'
        )
  )
ORDER BY fecha, numero_transaccion";

        try
        {
            return await context.Database
                .SqlQueryRaw<BancoCuentaConciliacionDto>(sql, companyId, bancoCuentaId, fechaDesde, fechaHasta)
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42703")
        {
            return await context.Database
                .SqlQueryRaw<BancoCuentaConciliacionDto>(sqlSinReferencia, companyId, bancoCuentaId, fechaDesde, fechaHasta)
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42P01")
        {
            var legacySql = @"
SELECT
    company_id AS ""CompanyId"",
    banco_cuenta_id AS ""BancoCuentaId"",
    numero_transaccion AS ""NumeroTransaccion"",
    fecha AS ""Fecha"",
    tipo AS ""Tipo"",
    NULL AS ""Referencia"",
    monto AS ""Monto"",
    NULL AS ""EstadoConciliacion""
FROM public.consolidar_cuenta_bancos
WHERE company_id = {0}
  AND banco_cuenta_id = {1}
  AND fecha >= {2}
  AND fecha <= {3}
ORDER BY fecha, numero_transaccion";
            return await context.Database
                .SqlQueryRaw<BancoCuentaConciliacionDto>(legacySql, companyId, bancoCuentaId, fechaDesde, fechaHasta)
                .ToListAsync(ct);
        }
    }


    public async Task<IReadOnlyList<BancoCuentaConciliacionDto>> GetConciliadasAsync(
        long companyId,
        long bancoCuentaId,
        DateOnly fechaDesde,
        DateOnly fechaHasta,
        CancellationToken ct = default)
    {
        var currentCompanyId = EnsureCompanyId();
        if (companyId <= 0 || companyId != currentCompanyId)
        {
            throw new InvalidOperationException("La empresa solicitada no es valida para el usuario actual.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);

        if (fechaDesde == default || fechaHasta == default)
        {
            throw new ArgumentException("Debe proporcionar un rango de fechas valido.");
        }

        if (fechaDesde > fechaHasta)
        {
            throw new ArgumentException("La fecha inicial no puede ser mayor que la fecha final.");
        }

        var sql = @"
SELECT
    company_id AS ""CompanyId"",
    banco_cuenta_id AS ""BancoCuentaId"",
    numero_transaccion AS ""NumeroTransaccion"",
    fecha AS ""Fecha"",
    tipo AS ""Tipo"",
    referencia AS ""Referencia"",
    monto AS ""Monto"",
    estado_conciliacion AS ""EstadoConciliacion""
FROM public.conciliacion_cuenta_bancos
WHERE company_id = {0}
  AND banco_cuenta_id = {1}
  AND fecha >= {2}
  AND fecha <= {3}
  AND UPPER(COALESCE(estado_conciliacion, 'NOC')) = 'CON'
ORDER BY fecha DESC, numero_transaccion DESC";

        var sqlSinReferencia = @"
SELECT
    company_id AS ""CompanyId"",
    banco_cuenta_id AS ""BancoCuentaId"",
    numero_transaccion AS ""NumeroTransaccion"",
    fecha AS ""Fecha"",
    tipo AS ""Tipo"",
    NULL AS ""Referencia"",
    monto AS ""Monto"",
    estado_conciliacion AS ""EstadoConciliacion""
FROM public.conciliacion_cuenta_bancos
WHERE company_id = {0}
  AND banco_cuenta_id = {1}
  AND fecha >= {2}
  AND fecha <= {3}
  AND UPPER(COALESCE(estado_conciliacion, 'NOC')) = 'CON'
ORDER BY fecha DESC, numero_transaccion DESC";

        try
        {
            return await context.Database
                .SqlQueryRaw<BancoCuentaConciliacionDto>(sql, companyId, bancoCuentaId, fechaDesde, fechaHasta)
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState == "42703"
                                           && string.Equals(ex.ColumnName, "referencia", StringComparison.OrdinalIgnoreCase))
        {
            return await context.Database
                .SqlQueryRaw<BancoCuentaConciliacionDto>(sqlSinReferencia, companyId, bancoCuentaId, fechaDesde, fechaHasta)
                .ToListAsync(ct);
        }
        catch (PostgresException ex) when (ex.SqlState is "42P01" or "42703")
        {
            return Array.Empty<BancoCuentaConciliacionDto>();
        }
    }
    public async Task ConciliarAsync(
        long companyId,
        long bancoCuentaId,
        string user,
        DateOnly fechaConciliacion,
        IReadOnlyList<BancoCuentaConciliacionDto> movimientos,
        CancellationToken ct = default)
    {
        var currentCompanyId = EnsureCompanyId();
        if (companyId <= 0 || companyId != currentCompanyId)
        {
            throw new InvalidOperationException("La empresa solicitada no es valida para el usuario actual.");
        }

        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bancoCuentaId);

        if (fechaConciliacion == default)
        {
            throw new ArgumentException("Debe proporcionar una fecha de conciliacion valida.", nameof(fechaConciliacion));
        }

        if (movimientos is null || movimientos.Count == 0)
        {
            throw new ArgumentException("Debe seleccionar movimientos para conciliar.", nameof(movimientos));
        }

        var existeCuenta = await context.ban_cuenta
            .AsNoTracking()
            .AnyAsync(c => c.company_id == companyId && c.banco_cuenta_id == bancoCuentaId, ct);

        if (!existeCuenta)
        {
            throw new ArgumentException("La cuenta bancaria no existe en la empresa actual.", nameof(bancoCuentaId));
        }

        var payload = JsonSerializer.Serialize(
            movimientos.Select(m => new Dictionary<string, object?>
            {
                ["numero_transaccion"] = m.NumeroTransaccion,
                ["referencia"] = m.Referencia,
                ["fecha"] = m.Fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                ["monto"] = m.Monto
            }));

        var connection = (NpgsqlConnection)context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandType = CommandType.Text;
            command.CommandText = @"
CALL public.sp_ban_kardex_conciliar(
    @p_company_id,
    @p_banco_cuenta_id,
    @p_usuario,
    @p_fecha_conciliacion,
    @p_movimientos
);";

            command.Parameters.AddWithValue("p_company_id", NpgsqlDbType.Bigint, companyId);
            command.Parameters.AddWithValue("p_banco_cuenta_id", NpgsqlDbType.Bigint, bancoCuentaId);
            command.Parameters.AddWithValue("p_usuario", NpgsqlDbType.Varchar, NormalizeUser(user));
            command.Parameters.AddWithValue("p_fecha_conciliacion", NpgsqlDbType.Date, fechaConciliacion);
            command.Parameters.Add(new NpgsqlParameter("p_movimientos", NpgsqlDbType.Jsonb)
            {
                Value = payload
            });

            await command.ExecuteNonQueryAsync(ct);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasContablesAsync(long companyId, CancellationToken ct = default)
    {
        var currentCompanyId = EnsureCompanyId();
        if (companyId <= 0 || companyId != currentCompanyId)
        {
            throw new InvalidOperationException("La empresa solicitada no es v?lida para el usuario actual.");
        }

        var config = await context.ban_config
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .Select(c => new { c.consolidado, c.cuenta_mayor })
            .FirstOrDefaultAsync(ct);

        if (config is null || !config.consolidado)
        {
            return Array.Empty<CuentaContableLookupDto>();
        }

        var cuentaMayor = config.cuenta_mayor?.Trim();
        if (string.IsNullOrWhiteSpace(cuentaMayor))
        {
            return Array.Empty<CuentaContableLookupDto>();
        }

        // Subarbol del mayor de bancos: ancla por code exacto y baja por parent_account_id
        // a cualquier profundidad (submayores). Sin LIKE. NO se filtra allows_posting aqui
        // para no podar la recursion en submayores no posteables.
        var sql = @"
WITH RECURSIVE arbol AS (
    SELECT account_id
    FROM public.con_plan_cuentas
    WHERE company_id = {0} AND code = {1}

    UNION ALL

    SELECT h.account_id
    FROM public.con_plan_cuentas h
    JOIN arbol a ON h.parent_account_id = a.account_id
    WHERE h.company_id = {0}
)
SELECT cpc.*
FROM public.con_plan_cuentas cpc
WHERE cpc.account_id IN (SELECT account_id FROM arbol)";

        var cuentas = await context.con_plan_cuentas
            .FromSqlRaw(sql, companyId, cuentaMayor)
            .AsNoTracking()
            .Where(c => c.allows_posting)
            .OrderBy(c => c.code)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);

        var format = await accountFormatService.GetFormatAsync(ct);
        foreach (var cuenta in cuentas)
        {
            cuenta.DisplayText = format.FormatDisplay(cuenta.Code, cuenta.Description);
        }

        return cuentas;
    }

    public async Task<BancoCuentaEditDto?> GetByIdAsync(long cuentaId, CancellationToken ct = default)
    {
        if (cuentaId <= 0)
        {
            return null;
        }

        var companyId = EnsureCompanyId();

        var entity = await context.ban_cuenta
            .AsNoTracking()
            .Include(c => c.ban_banco)
            .Where(c => c.company_id == companyId && c.banco_cuenta_id == cuentaId)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : MapToEditDto(entity);
    }

    public async Task<BancoCuentaEditDto> CreateAsync(BancoCuentaCreateDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var companyId = EnsureCompanyId();
        await ValidarBancoAsync(companyId, dto.BancoId, ct);
        var consolidado = await IsConsolidadoAsync(companyId, ct);
        var contAccountId = consolidado ? dto.ContAccountId : null;
        var ctaConc = consolidado
            ? await ObtenerCuentaContableDisplayAsync(companyId, contAccountId, ct)
            : NormalizeCuentaConcManual(dto.CtaConc);

        var numeroCuenta = NormalizeRequired(dto.NumeroCuenta, 50, nameof(BancoCuentaCreateDto.NumeroCuenta));
        await ValidarNumeroCuentaAsync(companyId, numeroCuenta, null, ct);

        var saldoActual = NormalizeSaldoActual(dto.SaldoActual);

        var entity = new ban_cuenta
        {
            company_id = companyId,
            ban_banco_id = dto.BancoId,
            code = await GenerateAccountCodeAsync(companyId, ct),
            numero_cuenta = numeroCuenta,
            tipo = NormalizeRequired(dto.TipoCuenta, 20, nameof(BancoCuentaCreateDto.TipoCuenta)).ToUpperInvariant(),
            currency_code = NormalizeCurrency(dto.Moneda),
            nombre = NormalizeOptional(dto.Titular, TitularMaxLength, nameof(BancoCuentaCreateDto.Titular)) ?? string.Empty,
            banco_nombre = NormalizeObservaciones(dto.Observaciones),
            activo = dto.Activo,
            estado = dto.Activo ? "ACTIVE" : "INACTIVE",
            allow_reconciliation = true,
            saldo_inicial = saldoActual,
            saldo_actual = saldoActual,
            created_at = DateTime.UtcNow,
            created_by = NormalizeUser(user),
            cont_account_id = contAccountId,
            cta_conc = int.TryParse(ctaConc, out var ctaConcInt) ? ctaConcInt : 0
        };

        context.ban_cuenta.Add(entity);
        await context.SaveChangesAsync(ct);
        await context.Entry(entity).Reference(e => e.ban_banco).LoadAsync(ct);

        return MapToEditDto(entity);
    }

    public async Task<BancoCuentaEditDto> UpdateAsync(long cuentaId, BancoCuentaEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (cuentaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cuentaId), "La cuenta bancaria no es válida.");
        }

        var companyId = EnsureCompanyId();

        var entity = await context.ban_cuenta
            .FirstOrDefaultAsync(c => c.company_id == companyId && c.banco_cuenta_id == cuentaId, ct)
            ?? throw new KeyNotFoundException("La cuenta bancaria no existe.");

        await ValidarBancoAsync(companyId, dto.BancoId, ct);
        var consolidado = await IsConsolidadoAsync(companyId, ct);
        var contAccountId = consolidado ? dto.ContAccountId : null;
        var ctaConc = consolidado
            ? await ObtenerCuentaContableDisplayAsync(companyId, contAccountId, ct)
            : NormalizeCuentaConcManual(dto.CtaConc);

        var numeroCuenta = NormalizeRequired(dto.NumeroCuenta, 50, nameof(BancoCuentaCreateDto.NumeroCuenta));
        await ValidarNumeroCuentaAsync(companyId, numeroCuenta, cuentaId, ct);

        var saldoActual = NormalizeSaldoActual(dto.SaldoActual);

        entity.ban_banco_id = dto.BancoId;
        entity.numero_cuenta = numeroCuenta;
        entity.tipo = NormalizeRequired(dto.TipoCuenta, 20, nameof(BancoCuentaCreateDto.TipoCuenta)).ToUpperInvariant();
        entity.currency_code = NormalizeCurrency(dto.Moneda);
        entity.nombre = NormalizeOptional(dto.Titular, TitularMaxLength, nameof(BancoCuentaCreateDto.Titular)) ?? string.Empty;
        entity.banco_nombre = NormalizeObservaciones(dto.Observaciones);
        entity.activo = dto.Activo;
        entity.estado = dto.Activo ? "ACTIVE" : "INACTIVE";
        entity.saldo_actual = saldoActual;
        entity.updated_at = DateTime.UtcNow;
        entity.updated_by = NormalizeUser(user);
        entity.cont_account_id = contAccountId;
        entity.cta_conc = int.TryParse(ctaConc, out var ctaConcInt) ? ctaConcInt : 0;

        await context.SaveChangesAsync(ct);
        await context.Entry(entity).Reference(e => e.ban_banco).LoadAsync(ct);

        return MapToEditDto(entity);
    }

    public async Task DeleteAsync(long cuentaId, CancellationToken ct = default)
    {
        if (cuentaId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(cuentaId), "La cuenta bancaria no es válida.");
        }

        var companyId = EnsureCompanyId();

        var entity = await context.ban_cuenta
            .FirstOrDefaultAsync(c => c.company_id == companyId && c.banco_cuenta_id == cuentaId, ct)
            ?? throw new KeyNotFoundException("La cuenta bancaria no existe.");

        var tieneMovimientos = await context.ban_movimiento
            .AsNoTracking()
            .AnyAsync(m => m.company_id == companyId && m.banco_cuenta_id == cuentaId, ct);

        if (tieneMovimientos)
        {
            throw new InvalidOperationException("La cuenta tiene movimientos registrados y no puede eliminarse.");
        }

        var tieneTransitos = await context.ban_movimiento_transito
            .AsNoTracking()
            .AnyAsync(m => m.company_id == companyId && m.banco_cuenta_id == cuentaId, ct);

        if (tieneTransitos)
        {
            throw new InvalidOperationException("La cuenta tiene movimientos en tránsito y no puede eliminarse.");
        }

        context.ban_cuenta.Remove(entity);
        await context.SaveChangesAsync(ct);
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

    private static string NormalizeCurrency(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "HNL";
        }

        var trimmed = value.Trim().ToUpperInvariant();
        if (trimmed.Length != 3)
        {
            throw new ArgumentException("La moneda debe tener exactamente 3 caracteres.", nameof(value));
        }

        return trimmed;
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }

    private static string? NormalizeObservaciones(string? value)
    {
        return NormalizeOptional(value, ObservacionesMaxLength, nameof(BancoCuentaCreateDto.Observaciones));
    }

    private async Task ValidarBancoAsync(long companyId, long bancoId, CancellationToken ct)
    {
        var existe = await context.ban_banco
            .AsNoTracking()
            .AnyAsync(b => b.company_id == companyId && b.ban_banco_id == bancoId, ct);

        if (!existe)
        {
            throw new ArgumentException("El banco seleccionado no existe en la empresa actual.", nameof(bancoId));
        }
    }

    private async Task<bool> IsConsolidadoAsync(long companyId, CancellationToken ct)
    {
        return await context.ban_config
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .Select(c => c.consolidado)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<string> ObtenerCuentaContableDisplayAsync(long companyId, long? cuentaContableId, CancellationToken ct)
    {
        if (!cuentaContableId.HasValue || cuentaContableId.Value <= 0)
        {
            throw new ArgumentException("Seleccione una cuenta contable valida.", nameof(cuentaContableId));
        }

        var cuenta = await context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.account_id == cuentaContableId.Value)
            .Select(c => new { c.code, c.name })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null)
        {
            throw new ArgumentException("La cuenta contable seleccionada no pertenece a la empresa actual.", nameof(cuentaContableId));
        }

        var format = await accountFormatService.GetFormatAsync(ct);
        return format.FormatDisplay(cuenta.code, cuenta.name);
    }

    private static string? NormalizeCuentaConcManual(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        foreach (var character in trimmed)
        {
            if (!char.IsDigit(character))
            {
                throw new ArgumentException("La cuenta contable debe ser numerica.", nameof(value));
            }
        }

        if (trimmed.Length > 255)
        {
            throw new ArgumentException("La cuenta contable supera el maximo permitido.", nameof(value));
        }

        return trimmed;
    }

    private async Task ValidarNumeroCuentaAsync(long companyId, string numeroCuenta, long? cuentaId, CancellationToken ct)
    {
        var normalized = numeroCuenta.ToUpperInvariant();

        var existe = await context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.company_id == companyId && (!cuentaId.HasValue || c.banco_cuenta_id != cuentaId.Value))
            .AnyAsync(c => c.numero_cuenta.ToUpper() == normalized, ct);

        if (existe)
        {
            throw new InvalidOperationException("Ya existe una cuenta con el mismo número.");
        }
    }

    private async Task<string> GenerateAccountCodeAsync(long companyId, CancellationToken ct)
    {
        var codes = await context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .Select(c => c.code)
            .ToListAsync(ct);

        var used = new HashSet<long>();
        foreach (var code in codes)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (long.TryParse(code.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                used.Add(value);
            }
        }

        var next = 1L;
        while (used.Contains(next))
        {
            next++;
        }

        return next.ToString("D6", CultureInfo.InvariantCulture);
    }

    private static BancoCuentaEditDto MapToEditDto(ban_cuenta entity)
    {
        return new BancoCuentaEditDto
        {
            BancoCuentaId = entity.banco_cuenta_id,
            CompanyId = entity.company_id,
            BancoId = entity.ban_banco_id ?? 0,
            ContAccountId = entity.cont_account_id,
            NumeroCuenta = entity.numero_cuenta,
            TipoCuenta = entity.tipo,
            Moneda = entity.currency_code,
            SaldoActual = entity.saldo_actual,
            Titular = string.IsNullOrWhiteSpace(entity.nombre) ? null : entity.nombre,
            Observaciones = string.IsNullOrWhiteSpace(entity.banco_nombre) ? null : entity.banco_nombre,
            Activo = entity.activo,
            CreatedAt = entity.created_at,
            CreatedBy = entity.created_by,
            UpdatedAt = entity.updated_at,
            UpdatedBy = entity.updated_by,
            CtaConc = entity.cta_conc.HasValue ? entity.cta_conc.Value.ToString() : null
        };
    }

    private static decimal NormalizeSaldoActual(decimal? saldoActual)
    {
        if (!saldoActual.HasValue)
        {
            throw new ArgumentException("El saldo actual es obligatorio.", nameof(saldoActual));
        }

        if (saldoActual.Value < 0)
        {
            throw new ArgumentException("El saldo actual no puede ser negativo.", nameof(saldoActual));
        }

        return saldoActual.Value;
    }
}