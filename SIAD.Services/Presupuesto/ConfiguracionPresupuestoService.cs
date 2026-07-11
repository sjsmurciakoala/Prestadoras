using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Presupuesto;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Presupuesto;

public sealed class ConfiguracionPresupuestoService : IConfiguracionPresupuestoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public ConfiguracionPresupuestoService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<string> GetNextIdAsync(CancellationToken ct = default)
    {
        return await GetNextGenericIdAsync(ct);
    }

    public async Task<string> GetNextIdAsync(string cuentaContable, CancellationToken ct = default)
    {
        var cuentaCode = NormalizeRequired(cuentaContable, "cuenta contable");
        return await GenerateNextIdForCuentaAsync(cuentaCode, ct);
    }

    public async Task<IReadOnlyList<ConfiguracionPresupuestoListItemDto>> GetAsync(
        ConfiguracionPresupuestoFilterDto? filtro,
        CancellationToken ct = default)
    {
        var query = BuildListQuery(filtro);
        var items = await query
            .OrderBy(c => c.IdPresupuesto)
            .ThenBy(c => c.CuentaContableCodigo)
            .Select(c => new ConfiguracionPresupuestoListItemDto
            {
                IdPresupuesto = c.IdPresupuesto,
                CuentaContableCodigo = c.CuentaContableCodigo,
                CuentaContable = c.CuentaContableCodigo,
                ValorGlobal = c.ValorGlobal,
                ValorDisponible = c.ValorDisponible,
                ValorProyeccion = c.ValorProyeccion,
                ValorReal = c.ValorReal,
                RangoPeriodo = c.RangoPeriodo,
                FechaInicia = c.FechaInicia,
                FechaFinaliza = c.FechaFinaliza,
                EstadoAprobado = c.EstadoAprobado
            })
            .ToListAsync(ct);

        await ApplyCuentaContableDisplayAsync(items, ct);
        return items;
    }

    public async Task<PagedResult<ConfiguracionPresupuestoListItemDto>> GetPagedAsync(
        ConfiguracionPresupuestoFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        if (skip < 0)
        {
            skip = 0;
        }

        if (take <= 0)
        {
            take = 50;
        }

        if (take > 500)
        {
            take = 500;
        }

        var query = BuildListQuery(filtro);
        var totalCount = await query.CountAsync(ct);
        query = ApplySort(query, sortField, sortDesc);

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(c => new ConfiguracionPresupuestoListItemDto
            {
                IdPresupuesto = c.IdPresupuesto,
                CuentaContableCodigo = c.CuentaContableCodigo,
                CuentaContable = c.CuentaContableCodigo,
                ValorGlobal = c.ValorGlobal,
                ValorDisponible = c.ValorDisponible,
                ValorProyeccion = c.ValorProyeccion,
                ValorReal = c.ValorReal,
                RangoPeriodo = c.RangoPeriodo,
                FechaInicia = c.FechaInicia,
                FechaFinaliza = c.FechaFinaliza,
                EstadoAprobado = c.EstadoAprobado
            })
            .ToListAsync(ct);

        await ApplyCuentaContableDisplayAsync(items, ct);
        return new PagedResult<ConfiguracionPresupuestoListItemDto>(items, totalCount);
    }

    public async Task<IReadOnlyList<ConfiguracionPresupuestoDetalleListItemDto>> GetDetailsByPresupuestoAsync(
        string idPresupuesto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return Array.Empty<ConfiguracionPresupuestoDetalleListItemDto>();
        }

        var id = idPresupuesto.Trim();
        var items = await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .Where(d => d.id_presupuesto == id)
            .OrderBy(d => d.con_cuenta_code)
            .Select(d => new ConfiguracionPresupuestoDetalleListItemDto
            {
                IdPresupuesto = d.id_presupuesto,
                CuentaContableCodigo = d.con_cuenta_code,
                CuentaContable = d.con_cuenta_code,
                ValorProyeccion = d.valor_proyeccion,
                ValorReal = d.valor_real,
                ValorDisponible = d.valor_disponible
            })
            .ToListAsync(ct);

        await ApplyCuentaContableDisplayAsync(items, ct);
        return items;
    }

    public async Task<ConfiguracionPresupuestoDetalleListItemDto?> GetDetailByIdAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto) || string.IsNullOrWhiteSpace(cuentaContable))
        {
            return null;
        }

        var id = idPresupuesto.Trim();
        var cuenta = cuentaContable.Trim();
        var detail = await GetPresupuestoDetailAsync(id, cuenta, ct);
        if (detail is null)
        {
            return null;
        }

        var result = new ConfiguracionPresupuestoDetalleListItemDto
        {
            IdPresupuesto = detail.id_presupuesto,
            CuentaContableCodigo = detail.con_cuenta_code,
            CuentaContable = detail.con_cuenta_code,
            ValorProyeccion = detail.valor_proyeccion,
            ValorReal = detail.valor_real,
            ValorDisponible = detail.valor_disponible
        };

        await ApplyCuentaContableDisplayAsync(new[] { result }, ct);
        return result;
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> GetCuentasDestinoTrasladoAsync(
        string idPresupuesto,
        string cuentaOrigen,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return Array.Empty<CuentaContableLookupDto>();
        }

        var id = idPresupuesto.Trim();
        var origen = cuentaOrigen?.Trim() ?? string.Empty;
        var companyId = EnsureCompanyId();
        var fechaActual = DateOnly.FromDateTime(DateTime.Today);

        var headerQuery = _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Where(h =>
                h.id_presupuesto == id &&
                h.estado_aprobado &&
                h.fecha_inicia <= fechaActual &&
                h.fecha_finaliza >= fechaActual);

        var header = await headerQuery.FirstOrDefaultAsync(ct);
        if (header is null)
        {
            return Array.Empty<CuentaContableLookupDto>();
        }

        if (!header.estado_aprobado)
        {
            throw new InvalidOperationException("El presupuesto no esta aprobado para gestionar solicitudes.");
        }

        var detailQuery = _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .Where(d => d.id_presupuesto == id);

        if (!string.IsNullOrWhiteSpace(origen))
        {
            if (_context.Database.IsRelational())
            {
                detailQuery = detailQuery.Where(d => !EF.Functions.ILike(d.con_cuenta_code, origen));
            }
            else
            {
                var origenUpper = origen.ToUpperInvariant();
                detailQuery = detailQuery.Where(d => d.con_cuenta_code.ToUpper() != origenUpper);
            }
        }

        var detailCodes = await detailQuery
            .Select(d => d.con_cuenta_code)
            .Distinct()
            .ToListAsync(ct);

        if (detailCodes.Count == 0)
        {
            return Array.Empty<CuentaContableLookupDto>();
        }

        var detailCodesUpper = detailCodes
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.ToUpperInvariant())
            .Distinct()
            .ToArray();

        if (detailCodesUpper.Length == 0)
        {
            return Array.Empty<CuentaContableLookupDto>();
        }

        var cuentas = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c =>
                c.company_id == companyId &&
                c.allows_budget &&
                detailCodesUpper.Contains(c.code.ToUpper()))
            .OrderBy(c => c.code)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);

        return cuentas;
    }

    public async Task<ConfiguracionPresupuestoDetalleListItemDto> AddDetailAsync(
        string idPresupuesto,
        ConfiguracionPresupuestoDetalleEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        var id = idPresupuesto.Trim();
        var cuentaContable = NormalizeRequired(dto.CuentaContable, "cuenta contable");

        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.id_presupuesto == id, ct);

        if (header is null)
        {
            throw new KeyNotFoundException("La configuracion de presupuesto no existe.");
        }

        EnsurePresupuestoVigente(header);

        if (header.estado_aprobado)
        {
            throw new InvalidOperationException("No se puede agregar detalle porque el presupuesto ya esta aprobado.");
        }

        var companyId = EnsureCompanyId();
        var accountType = await GetAccountTypeByCuentaCodeAsync(companyId, cuentaContable, ct);
        if (string.IsNullOrWhiteSpace(accountType))
        {
            throw new InvalidOperationException(
                "La cuenta contable seleccionada no existe o no permite presupuesto en el plan de cuentas.");
        }

        var exists = await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .AnyAsync(c =>
                c.id_presupuesto == id &&
                c.con_cuenta_code == cuentaContable, ct);

        if (exists)
        {
            throw new InvalidOperationException("Ya existe un detalle para la cuenta contable indicada.");
        }

        ValidateDetalleContraPresupuestoHdr(dto.ValorProyeccion, header.valor_global);
        await ValidateIncrementoTotalContraPresupuestoHdrAsync(
            id,
            header.valor_global,
            dto.ValorProyeccion,
            0m,
            ct);

        var detail = new pst_config_presupuesto_dtl
        {
            id_presupuesto = id,
            con_cuenta_code = cuentaContable,
            valor_proyeccion = dto.ValorProyeccion,
            valor_real = 0m,
            valor_disponible = CalculateValorDisponible(dto.ValorProyeccion, 0m)
        };

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        detail.id_presupuesto_dtl = await GetNextPresupuestoDetailIdAsync(id, ct);
        _context.pst_config_presupuesto_dtls.Add(detail);
        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        var result = new ConfiguracionPresupuestoDetalleListItemDto
        {
            IdPresupuesto = detail.id_presupuesto,
            CuentaContableCodigo = detail.con_cuenta_code,
            CuentaContable = detail.con_cuenta_code,
            ValorProyeccion = detail.valor_proyeccion,
            ValorReal = detail.valor_real,
            ValorDisponible = detail.valor_disponible
        };

        await ApplyCuentaContableDisplayAsync(new[] { result }, ct);
        return result;
    }

    public async Task<ConfiguracionPresupuestoDetalleListItemDto> UpdateDetailAsync(
        string idPresupuesto,
        string cuentaContable,
        ConfiguracionPresupuestoDetalleUpdateDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = idPresupuesto.Trim();
        var cuenta = cuentaContable.Trim();
        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.id_presupuesto == id, ct);

        if (header is null)
        {
            throw new KeyNotFoundException("La configuracion de presupuesto no existe.");
        }

        EnsurePresupuestoVigente(header);

        var detail = await GetPresupuestoDetailAsync(id, cuenta, ct);
        if (detail is null)
        {
            throw new KeyNotFoundException("El detalle del presupuesto no existe.");
        }

        if (header.estado_aprobado)
        {
            throw new InvalidOperationException("No se puede editar el detalle porque el presupuesto ya esta aprobado.");
        }

        ValidateDetalleContraPresupuestoHdr(dto.ValorProyeccion, header.valor_global);
        await ValidateIncrementoTotalContraPresupuestoHdrAsync(
            id,
            header.valor_global,
            dto.ValorProyeccion,
            detail.valor_proyeccion,
            ct);

        detail.valor_proyeccion = dto.ValorProyeccion;
        detail.valor_disponible = CalculateValorDisponible(detail.valor_proyeccion, detail.valor_real);
        await _context.SaveChangesAsync(ct);

        var result = new ConfiguracionPresupuestoDetalleListItemDto
        {
            IdPresupuesto = detail.id_presupuesto,
            CuentaContableCodigo = detail.con_cuenta_code,
            CuentaContable = detail.con_cuenta_code,
            ValorProyeccion = detail.valor_proyeccion,
            ValorReal = detail.valor_real,
            ValorDisponible = detail.valor_disponible
        };

        await ApplyCuentaContableDisplayAsync(new[] { result }, ct);
        return result;
    }

    public async Task<ConfiguracionPresupuestoEditDto?> GetByIdAsync(
        string idPresupuesto,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return null;
        }

        var id = idPresupuesto.Trim();
        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.id_presupuesto == id, ct);

        return header is null ? null : MapToEdit(header);
    }

    public async Task<ConfiguracionPresupuestoEditDto?> GetByIdAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            return null;
        }

        return await GetByIdAsync(idPresupuesto, ct);
    }

    public async Task<ConfiguracionPresupuestoEditDto> CreateAsync(
        ConfiguracionPresupuestoEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateDto(dto);

        if (dto.RangoPeriodo != 12)
        {
            throw new InvalidOperationException("El rango de periodo para crear presupuesto es fijo en 12 meses.");
        }

        var id = string.IsNullOrWhiteSpace(dto.IdPresupuesto)
            ? await GetNextGenericIdAsync(ct)
            : dto.IdPresupuesto.Trim();

        var exists = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .AnyAsync(c => c.id_presupuesto == id, ct);

        if (exists)
        {
            throw new InvalidOperationException("Ya existe una configuracion con ese ID de presupuesto.");
        }

        await ValidateNoPendingApprovalBeforeOrEqualRangeAsync(dto.FechaInicia, dto.FechaFinaliza, ct);
        await ValidateNoStartDateOverlapAsync(dto.FechaInicia, ct);

        var header = new pst_config_presupuesto_hdr
        {
            id_presupuesto = id,
            valor_global = dto.ValorGlobal,
            valor_disponible = dto.ValorGlobal,
            rango_periodo = dto.RangoPeriodo,
            fecha_inicia = dto.FechaInicia,
            fecha_finaliza = dto.FechaFinaliza,
            estado_aprobado = false
        };

        var companyId = EnsureCompanyId();
        var cuentasPresupuestables = await GetPresupuestoCuentaCodesAsync(companyId, ct);
        var proyeccionesPeriodoAnterior = await ObtenerProyeccionesPeriodoAnteriorAsync(dto.FechaInicia, ct);

        var detalles = cuentasPresupuestables
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(c => new pst_config_presupuesto_dtl
            {
                id_presupuesto = id,
                con_cuenta_code = c,
                valor_proyeccion = proyeccionesPeriodoAnterior.TryGetValue(c, out var valorAnterior)
                    ? valorAnterior
                    : 0m,
                valor_real = 0m,
                valor_disponible = proyeccionesPeriodoAnterior.TryGetValue(c, out var valorDisponibleAnterior)
                    ? CalculateValorDisponible(valorDisponibleAnterior, 0m)
                    : 0m
            })
            .ToList();

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        await AssignPresupuestoDetailIdsAsync(id, detalles, ct);
        _context.pst_config_presupuesto_hdrs.Add(header);
        if (detalles.Count > 0)
        {
            _context.pst_config_presupuesto_dtls.AddRange(detalles);
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return MapToEdit(header);
    }

    private async Task<Dictionary<string, decimal>> ObtenerProyeccionesPeriodoAnteriorAsync(
        DateOnly fechaIniciaNuevoPeriodo,
        CancellationToken ct)
    {
        var presupuestoAnteriorId = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Where(h => h.fecha_finaliza < fechaIniciaNuevoPeriodo)
            .OrderByDescending(h => h.fecha_finaliza)
            .ThenByDescending(h => h.fecha_inicia)
            .Select(h => h.id_presupuesto)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(presupuestoAnteriorId))
        {
            return new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        }

        var proyecciones = await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .Where(d => d.id_presupuesto == presupuestoAnteriorId)
            .Select(d => new { d.con_cuenta_code, d.valor_proyeccion })
            .ToListAsync(ct);

        return proyecciones
            .Where(d => !string.IsNullOrWhiteSpace(d.con_cuenta_code))
            .GroupBy(d => d.con_cuenta_code.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.Last().valor_proyeccion,
                StringComparer.OrdinalIgnoreCase);
    }

    public async Task<ConfiguracionPresupuestoEditDto> UpdateAsync(
        string idPresupuesto,
        ConfiguracionPresupuestoEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ValidateDto(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        var id = idPresupuesto.Trim();

        if (!string.Equals(id, dto.IdPresupuesto?.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No se permite cambiar el ID de presupuesto.");
        }

        var header = await _context.pst_config_presupuesto_hdrs
            .FirstOrDefaultAsync(c => c.id_presupuesto == id, ct);

        if (header is null)
        {
            throw new KeyNotFoundException("La configuracion de presupuesto no existe.");
        }

        await ValidateNoStartDateOverlapAsync(dto.FechaInicia, ct, id);

        if (dto.RangoPeriodo != header.rango_periodo)
        {
            throw new InvalidOperationException("No se permite modificar el rango de periodo del presupuesto.");
        }

        var valorRealActual = await GetTotalValorRealAsync(id, ct);
        if (dto.ValorGlobal < valorRealActual)
        {
            throw new InvalidOperationException(
                "El valor global no puede ser menor al monto ya ejecutado del presupuesto.");
        }

        header.rango_periodo = dto.RangoPeriodo;
        header.valor_global = dto.ValorGlobal;
        header.valor_disponible = CalculateValorDisponible(dto.ValorGlobal, valorRealActual);
        header.fecha_inicia = dto.FechaInicia;
        header.fecha_finaliza = dto.FechaFinaliza;
        if (dto.EstadoAprobado != header.estado_aprobado)
        {
            throw new InvalidOperationException(
                "El estado de aprobacion no se puede modificar desde edicion. Use la accion de aprobar presupuesto.");
        }

        await _context.SaveChangesAsync(ct);
        return MapToEdit(header);
    }

    public async Task<ConfiguracionPresupuestoEditDto> UpdateAsync(
        string idPresupuesto,
        string cuentaContable,
        ConfiguracionPresupuestoEditDto dto,
        string user,
        CancellationToken ct = default)
    {
        return await UpdateAsync(idPresupuesto, dto, user, ct);
    }

    public async Task<ConfiguracionPresupuestoEditDto> ApprovePresupuestoAsync(
        string idPresupuesto,
        string user,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        var id = idPresupuesto.Trim();
        var header = await _context.pst_config_presupuesto_hdrs
            .FirstOrDefaultAsync(h => h.id_presupuesto == id, ct);

        if (header is null)
        {
            throw new KeyNotFoundException("La configuracion de presupuesto no existe.");
        }

        if (header.estado_aprobado)
        {
            throw new InvalidOperationException("El presupuesto ya esta aprobado.");
        }

        var tieneProyeccionPositiva = await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .AnyAsync(d => d.id_presupuesto == id && d.valor_proyeccion > 0m, ct);

        if (!tieneProyeccionPositiva)
        {
            throw new InvalidOperationException(
                "No se puede aprobar el presupuesto: debe existir al menos un detalle con valor de proyeccion mayor a cero.");
        }

        var valorRealActual = await GetTotalValorRealAsync(id, ct);
        if (header.valor_global < valorRealActual)
        {
            throw new InvalidOperationException(
                "No se puede aprobar el presupuesto porque el monto ejecutado supera el valor global configurado.");
        }

        header.estado_aprobado = true;
        header.valor_disponible = CalculateValorDisponible(header.valor_global, valorRealActual);
        await _context.SaveChangesAsync(ct);

        return MapToEdit(header);
    }

    public async Task<bool> DeleteAsync(string idPresupuesto, string cuentaContable, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = idPresupuesto.Trim();
        var cuenta = cuentaContable.Trim();

        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.id_presupuesto == id, ct);

        if (header is not null && header.estado_aprobado)
        {
            throw new InvalidOperationException("No se puede eliminar detalle porque el presupuesto ya esta aprobado.");
        }

        var detail = await _context.pst_config_presupuesto_dtls
            .FirstOrDefaultAsync(c =>
                c.id_presupuesto == id &&
                c.con_cuenta_code == cuenta, ct);

        if (detail is null)
        {
            return false;
        }

        _context.pst_config_presupuesto_dtls.Remove(detail);

        var hasAnotherDetail = await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .AnyAsync(c =>
                c.id_presupuesto == id &&
                c.con_cuenta_code != detail.con_cuenta_code, ct);

        if (!hasAnotherDetail)
        {
            var headerToDelete = await _context.pst_config_presupuesto_hdrs
                .FirstOrDefaultAsync(c => c.id_presupuesto == id, ct);

            if (headerToDelete is not null)
            {
                _context.pst_config_presupuesto_hdrs.Remove(headerToDelete);
            }
        }

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<PresupuestoActividadSolicitudListItemDto>> GetSolicitudesByDetalleAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto) || string.IsNullOrWhiteSpace(cuentaContable))
        {
            return Array.Empty<PresupuestoActividadSolicitudListItemDto>();
        }

        var id = idPresupuesto.Trim();
        var cuenta = cuentaContable.Trim();
        var companyId = EnsureCompanyId();

        await EnsurePresupuestoAprobadoAsync(id, ct);

        var query = _context.pst_solicitud_actividad_presupuestos
            .AsNoTracking()
            .Where(s =>
                s.company_id == companyId &&
                s.id_presupuesto == id &&
                (s.cuenta_destino_code.ToUpper() == cuenta.ToUpper() ||
                 (s.cuenta_origen_code != null && s.cuenta_origen_code.ToUpper() == cuenta.ToUpper())))
            .OrderByDescending(s => s.solicitado_en)
            .ThenByDescending(s => s.solicitud_id);

        var items = await query
            .Select(s => new PresupuestoActividadSolicitudListItemDto
            {
                SolicitudId = s.solicitud_id,
                IdPresupuesto = s.id_presupuesto,
                TipoActividad = s.tipo_actividad,
                Estado = s.estado,
                CuentaOrigenCode = s.cuenta_origen_code,
                CuentaDestinoCode = s.cuenta_destino_code,
                Monto = s.monto,
                Prioridad = s.prioridad,
                Justificacion = s.justificacion,
                FechaNecesaria = s.fecha_necesaria,
                SolicitadoPor = s.solicitado_por,
                SolicitadoEn = s.solicitado_en,
                RevisadoPor = s.revisado_por,
                RevisadoEn = s.revisado_en,
                ComentarioRevision = s.comentario_revision,
                ActividadId = s.actividad_id
            })
            .ToListAsync(ct);

        return items;
    }

    public async Task<PresupuestoActividadSolicitudListItemDto> CreateSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        PresupuestoActividadSolicitudCreateDto dto,
        string user,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = idPresupuesto.Trim();
        var cuentaDetalle = cuentaContable.Trim();
        var tipoActividad = NormalizeTipoActividad(dto.TipoActividad);
        var companyId = EnsureCompanyId();

        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.id_presupuesto == id, ct);

        if (header is null)
        {
            throw new KeyNotFoundException("El presupuesto indicado no existe.");
        }

        await EnsurePresupuestoAprobadoAsync(id, ct);

        var detalleBase = await GetPresupuestoDetailAsync(id, cuentaDetalle, ct);
        if (detalleBase is null)
        {
            throw new InvalidOperationException("La cuenta base no existe en el detalle del presupuesto.");
        }

        string? cuentaOrigen = null;
        string cuentaDestino;

        if (tipoActividad == "AMPLIACION")
        {
            cuentaDestino = cuentaDetalle;
            var nuevoValorDestino = detalleBase.valor_proyeccion + dto.Monto;
            ValidateDetalleContraPresupuestoHdr(nuevoValorDestino, header.valor_global);
            await ValidateIncrementoTotalContraPresupuestoHdrAsync(
                id,
                header.valor_global,
                nuevoValorDestino,
                detalleBase.valor_proyeccion,
                ct);
        }
        else
        {
            cuentaOrigen = cuentaDetalle;
            cuentaDestino = NormalizeRequired(dto.CuentaDestinoCode, "cuenta destino");

            if (string.Equals(cuentaOrigen, cuentaDestino, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("La cuenta destino debe ser diferente a la cuenta origen.");
            }

            var detalleDestino = await GetPresupuestoDetailAsync(id, cuentaDestino, ct);
            if (detalleDestino is null)
            {
                throw new InvalidOperationException("La cuenta destino no existe en el detalle del presupuesto.");
            }

            if (detalleBase.valor_proyeccion < dto.Monto)
            {
                throw new InvalidOperationException("El monto a trasladar no puede superar el presupuesto de la cuenta origen.");
            }

            ValidateDetalleContraPresupuestoHdr(
                detalleDestino.valor_proyeccion + dto.Monto,
                header.valor_global);
        }

        var now = GetCurrentTimestampWithoutTimeZone();
        var solicitud = new pst_solicitud_actividad_presupuesto
        {
            company_id = companyId,
            id_presupuesto = id,
            tipo_actividad = tipoActividad,
            cuenta_origen_code = cuentaOrigen,
            cuenta_destino_code = cuentaDestino,
            monto = dto.Monto,
            justificacion = NormalizeRequired(dto.Justificacion, "justificacion"),
            prioridad = dto.Prioridad,
            estado = "PENDIENTE",
            solicitado_por = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim(),
            solicitado_en = now
        };

        _context.pst_solicitud_actividad_presupuestos.Add(solicitud);
        await _context.SaveChangesAsync(ct);

        return MapSolicitud(solicitud);
    }

    public async Task<PresupuestoActividadSolicitudListItemDto> ApproveSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        string user,
        string? comentario,
        CancellationToken ct = default)
    {
        var solicitud = await GetSolicitudForDecisionAsync(idPresupuesto, cuentaContable, solicitudId, ct);
        await EnsurePresupuestoAprobadoAsync(solicitud.id_presupuesto, ct);
        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.id_presupuesto == solicitud.id_presupuesto, ct)
            ?? throw new KeyNotFoundException("La configuracion de presupuesto no existe.");

        if (!string.Equals(solicitud.estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se pueden aprobar solicitudes en estado PENDIENTE.");
        }

        var now = GetCurrentTimestampWithoutTimeZone();
        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        var tipo = NormalizeTipoActividad(solicitud.tipo_actividad);

        var cuentaDestino = string.IsNullOrWhiteSpace(solicitud.cuenta_destino_code)
            ? throw new InvalidOperationException("La solicitud no tiene cuenta destino configurada.")
            : solicitud.cuenta_destino_code.Trim();
        var detalleDestino = await GetPresupuestoDetailAsync(solicitud.id_presupuesto, cuentaDestino, ct)
            ?? throw new InvalidOperationException("La cuenta destino no existe en el detalle del presupuesto.");

        pst_config_presupuesto_dtl? detalleOrigen = null;
        if (tipo == "TRASLADO")
        {
            var cuentaOrigen = solicitud.cuenta_origen_code?.Trim() ?? string.Empty;
            detalleOrigen = await GetPresupuestoDetailAsync(solicitud.id_presupuesto, cuentaOrigen, ct)
                ?? throw new InvalidOperationException("La cuenta origen no existe en el detalle del presupuesto.");

            if (detalleOrigen.valor_proyeccion < solicitud.monto)
            {
                throw new InvalidOperationException("La cuenta origen no tiene saldo suficiente para el traslado.");
            }
        }

        var nuevoValorDestino = detalleDestino.valor_proyeccion + solicitud.monto;
        ValidateDetalleContraPresupuestoHdr(nuevoValorDestino, header.valor_global);
        if (tipo == "AMPLIACION")
        {
            await ValidateIncrementoTotalContraPresupuestoHdrAsync(
                solicitud.id_presupuesto,
                header.valor_global,
                nuevoValorDestino,
                detalleDestino.valor_proyeccion,
                ct);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        if (tipo == "TRASLADO" && detalleOrigen is not null)
        {
            detalleOrigen.valor_proyeccion -= solicitud.monto;
            detalleOrigen.valor_disponible = CalculateValorDisponible(detalleOrigen.valor_proyeccion, detalleOrigen.valor_real);
        }

        detalleDestino.valor_proyeccion += solicitud.monto;
        detalleDestino.valor_disponible = CalculateValorDisponible(detalleDestino.valor_proyeccion, detalleDestino.valor_real);

        var actividad = new pst_actividad_presupuesto
        {
            company_id = solicitud.company_id,
            id_presupuesto = solicitud.id_presupuesto,
            tipo_actividad = solicitud.tipo_actividad,
            estado = "APLICADA",
            fecha_actividad = now,
            cuenta_origen_code = solicitud.cuenta_origen_code,
            cuenta_destino_code = cuentaDestino,
            monto = solicitud.monto,
            motivo = solicitud.justificacion,
            referencia = $"SOL-{solicitud.solicitud_id}",
            created_at = now,
            created_by = usuario,
            approved_at = now,
            approved_by = usuario,
            applied_at = now,
            applied_by = usuario
        };

        _context.pst_actividad_presupuestos.Add(actividad);

        solicitud.estado = "ATENDIDA";
        solicitud.revisado_por = usuario;
        solicitud.revisado_en = now;
        solicitud.comentario_revision = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();

        await _context.SaveChangesAsync(ct);

        solicitud.actividad_id = actividad.actividad_id;
        await _context.SaveChangesAsync(ct);

        await tx.CommitAsync(ct);
        return MapSolicitud(solicitud);
    }

    public async Task<PresupuestoActividadSolicitudListItemDto> RejectSolicitudAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        string user,
        string? comentario,
        CancellationToken ct = default)
    {
        var solicitud = await GetSolicitudForDecisionAsync(idPresupuesto, cuentaContable, solicitudId, ct);
        await EnsurePresupuestoAprobadoAsync(solicitud.id_presupuesto, ct);

        if (!string.Equals(solicitud.estado, "PENDIENTE", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Solo se pueden rechazar solicitudes en estado PENDIENTE.");
        }

        var now = GetCurrentTimestampWithoutTimeZone();
        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();

        solicitud.estado = "RECHAZADA";
        solicitud.revisado_por = usuario;
        solicitud.revisado_en = now;
        solicitud.comentario_revision = string.IsNullOrWhiteSpace(comentario) ? null : comentario.Trim();

        await _context.SaveChangesAsync(ct);
        return MapSolicitud(solicitud);
    }

    private IQueryable<PresupuestoConfigQueryRow> BuildDetailQuery(ConfiguracionPresupuestoFilterDto? filtro)
    {
        filtro ??= new ConfiguracionPresupuestoFilterDto();

        var query = from detail in _context.pst_config_presupuesto_dtls.AsNoTracking()
                    join header in _context.pst_config_presupuesto_hdrs.AsNoTracking()
                        on detail.id_presupuesto equals header.id_presupuesto
                    select new PresupuestoConfigQueryRow
                    {
                        IdPresupuesto = detail.id_presupuesto,
                        CuentaContableCodigo = detail.con_cuenta_code,
                        ValorGlobal = header.valor_global,
                        ValorDisponible = header.valor_disponible,
                        ValorProyeccion = detail.valor_proyeccion,
                        ValorReal = detail.valor_real,
                        RangoPeriodo = header.rango_periodo,
                        FechaInicia = header.fecha_inicia,
                        FechaFinaliza = header.fecha_finaliza,
                        EstadoAprobado = header.estado_aprobado
                    };

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(c =>
                    EF.Functions.ILike(c.IdPresupuesto, likePattern) ||
                    EF.Functions.ILike(c.CuentaContableCodigo, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(c =>
                    c.IdPresupuesto.ToLowerInvariant().Contains(lowered) ||
                    c.CuentaContableCodigo.ToLowerInvariant().Contains(lowered));
            }
        }

        return query;
    }

    private IQueryable<PresupuestoConfigQueryRow> BuildListQuery(ConfiguracionPresupuestoFilterDto? filtro)
    {
        filtro ??= new ConfiguracionPresupuestoFilterDto();

        var details = _context.pst_config_presupuesto_dtls.AsNoTracking();

        var query = _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Select(header => new PresupuestoConfigQueryRow
            {
                IdPresupuesto = header.id_presupuesto,
                CuentaContableCodigo = details
                    .Where(d => d.id_presupuesto == header.id_presupuesto)
                    .OrderBy(d => d.con_cuenta_code)
                    .Select(d => d.con_cuenta_code)
                    .FirstOrDefault() ?? string.Empty,
                ValorGlobal = header.valor_global,
                ValorDisponible = header.valor_disponible,
                ValorProyeccion = details
                    .Where(d => d.id_presupuesto == header.id_presupuesto)
                    .Select(d => (decimal?)d.valor_proyeccion)
                    .Sum() ?? 0m,
                ValorReal = details
                    .Where(d => d.id_presupuesto == header.id_presupuesto)
                    .Select(d => (decimal?)d.valor_real)
                    .Sum() ?? 0m,
                RangoPeriodo = header.rango_periodo,
                FechaInicia = header.fecha_inicia,
                FechaFinaliza = header.fecha_finaliza,
                EstadoAprobado = header.estado_aprobado
            });

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(c =>
                    EF.Functions.ILike(c.IdPresupuesto, likePattern) ||
                    details.Any(d =>
                        d.id_presupuesto == c.IdPresupuesto &&
                        EF.Functions.ILike(d.con_cuenta_code, likePattern)));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(c =>
                    c.IdPresupuesto.ToLowerInvariant().Contains(lowered) ||
                    details.Any(d =>
                        d.id_presupuesto == c.IdPresupuesto &&
                        d.con_cuenta_code.ToLowerInvariant().Contains(lowered)));
            }
        }

        return query;
    }

    private static IQueryable<PresupuestoConfigQueryRow> ApplySort(
        IQueryable<PresupuestoConfigQueryRow> query,
        string? sortField,
        bool sortDesc)
    {
        var field = NormalizeSortField(sortField);
        if (string.IsNullOrWhiteSpace(field))
        {
            return query.OrderByDescending(c => c.FechaInicia)
                .ThenByDescending(c => c.IdPresupuesto)
                .ThenByDescending(c => c.CuentaContableCodigo);
        }

        return field switch
        {
            "idpresupuesto" => sortDesc
                ? query.OrderByDescending(c => c.IdPresupuesto).ThenByDescending(c => c.CuentaContableCodigo)
                : query.OrderBy(c => c.IdPresupuesto).ThenBy(c => c.CuentaContableCodigo),
            "cuentacontable" => sortDesc
                ? query.OrderByDescending(c => c.CuentaContableCodigo)
                : query.OrderBy(c => c.CuentaContableCodigo),
            "valorglobal" => sortDesc
                ? query.OrderByDescending(c => c.ValorGlobal)
                : query.OrderBy(c => c.ValorGlobal),
            "valorproyeccion" => sortDesc
                ? query.OrderByDescending(c => c.ValorProyeccion)
                : query.OrderBy(c => c.ValorProyeccion),
            "valorreal" => sortDesc
                ? query.OrderByDescending(c => c.ValorReal)
                : query.OrderBy(c => c.ValorReal),
            "rangoperiodo" => sortDesc
                ? query.OrderByDescending(c => c.RangoPeriodo)
                : query.OrderBy(c => c.RangoPeriodo),
            "aniopresupuesto" => sortDesc
                ? query.OrderByDescending(c => c.FechaInicia)
                    .ThenByDescending(c => c.IdPresupuesto)
                    .ThenByDescending(c => c.CuentaContableCodigo)
                : query.OrderBy(c => c.FechaInicia)
                    .ThenBy(c => c.IdPresupuesto)
                    .ThenBy(c => c.CuentaContableCodigo),
            "fechainicia" => sortDesc
                ? query.OrderByDescending(c => c.FechaInicia)
                : query.OrderBy(c => c.FechaInicia),
            "fechafinaliza" => sortDesc
                ? query.OrderByDescending(c => c.FechaFinaliza)
                : query.OrderBy(c => c.FechaFinaliza),
            "estado" or "estadoaprobado" or "aprobacion" => sortDesc
                ? query.OrderByDescending(c => c.EstadoAprobado)
                : query.OrderBy(c => c.EstadoAprobado),
            "variacion" => sortDesc
                ? query.OrderByDescending(c => c.ValorProyeccion - c.ValorReal)
                : query.OrderBy(c => c.ValorProyeccion - c.ValorReal),
            _ => query.OrderByDescending(c => c.FechaInicia)
                .ThenByDescending(c => c.IdPresupuesto)
                .ThenByDescending(c => c.CuentaContableCodigo)
        };
    }

    private static string NormalizeSortField(string? field)
    {
        if (string.IsNullOrWhiteSpace(field))
        {
            return string.Empty;
        }

        return field.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
    }

    private static void ValidateDto(ConfiguracionPresupuestoEditDto dto)
    {
        if (dto.ValorGlobal <= 0m)
        {
            throw new ArgumentException("El presupuesto debe ser mayor a cero.", nameof(dto.ValorGlobal));
        }

        if (dto.RangoPeriodo <= 0)
        {
            throw new ArgumentException("El rango de periodo debe ser mayor a cero.", nameof(dto.RangoPeriodo));
        }

        var minFechaInicia = new DateOnly(DateTime.Today.Year, 1, 1);
        if (dto.FechaInicia < minFechaInicia)
        {
            throw new ArgumentException(
                $"La fecha inicia no puede ser anterior al inicio del anio en curso ({minFechaInicia:yyyy-MM-dd}).",
                nameof(dto.FechaInicia));
        }

        if (dto.FechaFinaliza < dto.FechaInicia)
        {
            throw new ArgumentException("La fecha finaliza no puede ser menor a la fecha inicia.", nameof(dto.FechaFinaliza));
        }
    }

    private static void ValidateDetalleContraPresupuestoHdr(decimal valorDetalle, decimal presupuestoHdr)
    {
        if (valorDetalle > presupuestoHdr)
        {
            throw new InvalidOperationException(
                "El valor proyectado del detalle no puede ser mayor al presupuesto del encabezado.");
        }
    }

    private async Task ValidateIncrementoTotalContraPresupuestoHdrAsync(
        string idPresupuesto,
        decimal presupuestoHdr,
        decimal nuevoValorDetalle,
        decimal valorActualDetalle,
        CancellationToken ct)
    {
        var totalActual = await GetTotalValorProyeccionAsync(idPresupuesto, ct);
        var totalNuevo = totalActual - valorActualDetalle + nuevoValorDetalle;

        if (totalNuevo > presupuestoHdr && totalNuevo > totalActual)
        {
            throw new InvalidOperationException(
                "El total proyectado del presupuesto no puede ser mayor al presupuesto del encabezado.");
        }
    }

    private async Task<decimal> GetTotalValorProyeccionAsync(string idPresupuesto, CancellationToken ct)
    {
        return await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .Where(d => d.id_presupuesto == idPresupuesto)
            .Select(d => (decimal?)d.valor_proyeccion)
            .SumAsync(ct) ?? 0m;
    }

    private static string NormalizeRequired(string? value, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.");
        }

        return value.Trim();
    }

    private static DateTime GetCurrentTimestampWithoutTimeZone()
    {
        // PostgreSQL "timestamp without time zone" should be written with Unspecified kind.
        return DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual de la sesion.");
        }

        return companyId;
    }

    private async Task<string> GenerateNextIdForCuentaAsync(string cuentaContable, CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var accountType = await GetAccountTypeByCuentaCodeAsync(companyId, cuentaContable, ct);

        if (string.IsNullOrWhiteSpace(accountType))
        {
            throw new InvalidOperationException(
                "La cuenta contable seleccionada no existe o no permite presupuesto en el plan de cuentas.");
        }

        var start = GetInitialSequenceByAccountType(accountType);
        return await GetNextIdByRangeAsync(start, ct);
    }

    private async Task<string?> GetAccountTypeByCuentaCodeAsync(long companyId, string cuentaContable, CancellationToken ct)
    {
        var query = _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.allows_budget);

        if (_context.Database.IsRelational())
        {
            return await query
                .Where(c => EF.Functions.ILike(c.code, cuentaContable))
                .Select(c => c.account_type)
                .FirstOrDefaultAsync(ct);
        }

        var normalized = cuentaContable.ToUpperInvariant();
        return await query
            .Where(c => c.code.ToUpperInvariant() == normalized)
            .Select(c => c.account_type)
            .FirstOrDefaultAsync(ct);
    }

    private static int GetInitialSequenceByAccountType(string accountType)
    {
        var normalized = accountType.Trim().ToUpperInvariant();
        return normalized switch
        {
            "ACTIVO" => 10000,
            "PASIVO" => 20000,
            "CAPITAL" => 30000,
            "PATRIMONIO" => 30000,
            "EGRESO" => 40000,
            "EGRESOS" => 40000,
            "GASTO" => 40000,
            "GASTOS" => 40000,
            "COSTO" => 50000,
            "COSTOS" => 50000,
            "INGRESO" => 60000,
            "INGRESOS" => 60000,
            "MEMORANDA" => 70000,
            _ => 10000
        };
    }

    private async Task<string> GetNextIdByRangeAsync(int sequenceStart, CancellationToken ct)
    {
        const int stepSize = 10000;

        var allIds = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Select(c => c.id_presupuesto)
            .ToListAsync(ct);

        var maxInRange = sequenceStart - 1;
        foreach (var rawId in allIds)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                continue;
            }

            if (!int.TryParse(rawId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (value < sequenceStart)
            {
                continue;
            }

            if (value > maxInRange)
            {
                maxInRange = value;
            }
        }

        var next = maxInRange < sequenceStart
            ? sequenceStart
            : GetNextIdByStep(maxInRange, stepSize);

        return next.ToString(CultureInfo.InvariantCulture);
    }

    private async Task<string> GetNextGenericIdAsync(CancellationToken ct)
    {
        var allIds = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Select(c => c.id_presupuesto)
            .ToListAsync(ct);

        var maxNumericId = 0;
        foreach (var rawId in allIds)
        {
            if (string.IsNullOrWhiteSpace(rawId))
            {
                continue;
            }

            if (!int.TryParse(rawId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (value > maxNumericId)
            {
                maxNumericId = value;
            }
        }

        const int stepSize = 10000;
        var next = maxNumericId <= 0
            ? stepSize
            : GetNextIdByStep(maxNumericId, stepSize);

        return next.ToString(CultureInfo.InvariantCulture);
    }

    private static int GetNextIdByStep(int currentValue, int stepSize)
    {
        var remainder = currentValue % stepSize;
        return remainder == 0
            ? currentValue + stepSize
            : currentValue + (stepSize - remainder);
    }

    private async Task<decimal> GetTotalValorRealAsync(string idPresupuesto, CancellationToken ct)
    {
        return await _context.pst_config_presupuesto_dtls
            .AsNoTracking()
            .Where(d => d.id_presupuesto == idPresupuesto)
            .Select(d => (decimal?)d.valor_real)
            .SumAsync(ct) ?? 0m;
    }

    private static decimal CalculateValorDisponible(decimal valorGlobal, decimal valorReal)
    {
        var disponible = valorGlobal - valorReal;
        return disponible <= 0m ? 0m : disponible;
    }

    private async Task AssignPresupuestoDetailIdsAsync(
        string idPresupuesto,
        IReadOnlyList<pst_config_presupuesto_dtl> detalles,
        CancellationToken ct)
    {
        if (detalles.Count == 0)
        {
            return;
        }

        var nextId = await GetNextPresupuestoDetailIdAsync(idPresupuesto, ct);
        foreach (var detalle in detalles)
        {
            detalle.id_presupuesto_dtl = nextId++;
        }
    }

    private async Task<long> GetNextPresupuestoDetailIdAsync(
        string idPresupuesto,
        CancellationToken ct)
    {
        var id = NormalizeRequired(idPresupuesto, "ID de presupuesto");

        if (!_context.Database.IsRelational())
        {
            var baseId = ParsePresupuestoDetailBaseId(id);
            var currentMax = await _context.pst_config_presupuesto_dtls
                .AsNoTracking()
                .Where(d => d.id_presupuesto == id)
                .Select(d => (long?)d.id_presupuesto_dtl)
                .MaxAsync(ct);

            return Math.Max(currentMax ?? baseId, baseId) + 1;
        }

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT public.fn_pst_next_id_presupuesto_dtl(@idPresupuesto);";

            var currentTransaction = _context.Database.CurrentTransaction;
            if (currentTransaction is not null)
            {
                command.Transaction = currentTransaction.GetDbTransaction();
            }

            var idParameter = command.CreateParameter();
            idParameter.ParameterName = "@idPresupuesto";
            idParameter.DbType = DbType.String;
            idParameter.Value = id;
            command.Parameters.Add(idParameter);

            var rawResult = await command.ExecuteScalarAsync(ct);
            if (rawResult is null || rawResult is DBNull)
            {
                throw new InvalidOperationException(
                    "No se pudo generar el siguiente correlativo de detalle del presupuesto.");
            }

            return Convert.ToInt64(rawResult, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (shouldCloseConnection && _context.Database.CurrentTransaction is null)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static long ParsePresupuestoDetailBaseId(string idPresupuesto)
    {
        if (!long.TryParse(idPresupuesto.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var baseId))
        {
            throw new InvalidOperationException(
                $"El presupuesto {idPresupuesto} no es numerico. No se puede generar id_presupuesto_dtl.");
        }

        return baseId;
    }

    private async Task ValidateNoStartDateOverlapAsync(
        DateOnly fechaInicia,
        CancellationToken ct,
        string? excludeIdPresupuesto = null)
    {
        var query = _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Where(h =>
                h.fecha_inicia <= fechaInicia &&
                h.fecha_finaliza >= fechaInicia);

        if (!string.IsNullOrWhiteSpace(excludeIdPresupuesto))
        {
            var exclude = excludeIdPresupuesto.Trim();
            query = query.Where(h => h.id_presupuesto != exclude);
        }

        var conflicto = await query
            .OrderByDescending(h => h.fecha_inicia)
            .Select(h => new { h.id_presupuesto, h.fecha_inicia, h.fecha_finaliza })
            .FirstOrDefaultAsync(ct);

        if (conflicto is not null)
        {
            throw new InvalidOperationException(
                $"No se puede registrar el presupuesto. La fecha inicial ({fechaInicia:yyyy-MM-dd}) " +
                $"esta dentro del presupuesto {conflicto.id_presupuesto} " +
                $"({conflicto.fecha_inicia:yyyy-MM-dd} - {conflicto.fecha_finaliza:yyyy-MM-dd}).");
        }
    }

    private async Task ApplyCuentaContableDisplayAsync(
        IReadOnlyList<ConfiguracionPresupuestoListItemDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        var companyId = EnsureCompanyId();
        var codes = items
            .Select(x => x.CuentaContableCodigo?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (codes.Length == 0)
        {
            return;
        }

        var normalizedCodes = codes
            .Select(x => x!.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var planItems = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && normalizedCodes.Contains(c.code.ToUpper()))
            .Select(c => new { c.code, c.name })
            .ToListAsync(ct);

        var displayMap = planItems
            .GroupBy(x => x.code ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(
                x => (x.code ?? string.Empty).Trim(),
                x =>
                {
                    var code = (x.code ?? string.Empty).Trim();
                    var name = (x.name ?? string.Empty).Trim();
                    return AccountCodeFormatter.FormatDisplay(code, name);
                },
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var code = item.CuentaContableCodigo?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (displayMap.TryGetValue(code, out var display) && !string.IsNullOrWhiteSpace(display))
            {
                item.CuentaContable = display;
                continue;
            }

            item.CuentaContable = code;
        }
    }

    private async Task ApplyCuentaContableDisplayAsync(
        IReadOnlyList<ConfiguracionPresupuestoDetalleListItemDto> items,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        var companyId = EnsureCompanyId();
        var codes = items
            .Select(x => x.CuentaContableCodigo?.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (codes.Length == 0)
        {
            return;
        }

        var normalizedCodes = codes
            .Select(x => x!.ToUpperInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var planItems = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && normalizedCodes.Contains(c.code.ToUpper()))
            .Select(c => new { c.code, c.name })
            .ToListAsync(ct);

        var displayMap = planItems
            .GroupBy(x => x.code ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(
                x => (x.code ?? string.Empty).Trim(),
                x =>
                {
                    var code = (x.code ?? string.Empty).Trim();
                    var name = (x.name ?? string.Empty).Trim();
                    return AccountCodeFormatter.FormatDisplay(code, name);
                },
                StringComparer.OrdinalIgnoreCase);

        foreach (var item in items)
        {
            var code = item.CuentaContableCodigo?.Trim();
            if (string.IsNullOrWhiteSpace(code))
            {
                continue;
            }

            if (displayMap.TryGetValue(code, out var display) && !string.IsNullOrWhiteSpace(display))
            {
                item.CuentaContable = display;
                continue;
            }

            item.CuentaContable = code;
        }
    }

    private static string NormalizeTipoActividad(string? tipoActividad)
    {
        var tipo = NormalizeRequired(tipoActividad, "tipo de actividad").ToUpperInvariant();
        if (tipo is "AMPLIACION" or "TRASLADO")
        {
            return tipo;
        }

        throw new InvalidOperationException("El tipo de actividad debe ser AMPLIACION o TRASLADO.");
    }

    private async Task EnsurePresupuestoAprobadoAsync(
        string idPresupuesto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("Debe indicar el presupuesto.");
        }

        var header = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.id_presupuesto == idPresupuesto.Trim(), ct);

        if (header is null)
        {
            throw new InvalidOperationException("El presupuesto no existe.");
        }

        EnsurePresupuestoVigente(header);

        if (!header.estado_aprobado)
        {
            throw new InvalidOperationException("El presupuesto no esta aprobado para gestionar solicitudes.");
        }
    }

    private async Task ValidateNoPendingApprovalBeforeOrEqualRangeAsync(
        DateOnly fechaInicia,
        DateOnly fechaFinaliza,
        CancellationToken ct)
    {
        var pendiente = await _context.pst_config_presupuesto_hdrs
            .AsNoTracking()
            .Where(h =>
                !h.estado_aprobado &&
                h.fecha_inicia <= fechaInicia &&
                h.fecha_finaliza <= fechaFinaliza)
            .OrderByDescending(h => h.fecha_finaliza)
            .ThenByDescending(h => h.fecha_inicia)
            .Select(h => new { h.id_presupuesto, h.fecha_inicia, h.fecha_finaliza })
            .FirstOrDefaultAsync(ct);

        if (pendiente is null)
        {
            return;
        }

        throw new InvalidOperationException(
            $"No se puede crear un nuevo presupuesto porque el presupuesto {pendiente.id_presupuesto} " +
            $"({pendiente.fecha_inicia:yyyy-MM-dd} - {pendiente.fecha_finaliza:yyyy-MM-dd}) " +
            "esta pendiente de aprobacion.");
    }

    private static void EnsurePresupuestoVigente(pst_config_presupuesto_hdr header)
    {
        var fechaActual = DateOnly.FromDateTime(DateTime.Today);
        if (header.fecha_inicia <= fechaActual && header.fecha_finaliza >= fechaActual)
        {
            return;
        }

        throw new InvalidOperationException(
            $"El presupuesto esta fuera del rango de fechas ({header.fecha_inicia:yyyy-MM-dd} al {header.fecha_finaliza:yyyy-MM-dd}).");
    }

    private async Task<pst_config_presupuesto_dtl?> GetPresupuestoDetailAsync(
        string idPresupuesto,
        string cuentaContable,
        CancellationToken ct)
    {
        if (_context.Database.IsRelational())
        {
            return await _context.pst_config_presupuesto_dtls
                .FirstOrDefaultAsync(d =>
                    d.id_presupuesto == idPresupuesto &&
                    EF.Functions.ILike(d.con_cuenta_code, cuentaContable), ct);
        }

        var cuenta = cuentaContable.ToUpperInvariant();
        return await _context.pst_config_presupuesto_dtls
            .FirstOrDefaultAsync(d =>
                d.id_presupuesto == idPresupuesto &&
                d.con_cuenta_code.ToUpper() == cuenta, ct);
    }

    private async Task<pst_solicitud_actividad_presupuesto> GetSolicitudForDecisionAsync(
        string idPresupuesto,
        string cuentaContable,
        long solicitudId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(idPresupuesto))
        {
            throw new ArgumentException("El ID de presupuesto no es valido.", nameof(idPresupuesto));
        }

        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            throw new ArgumentException("La cuenta contable no es valida.", nameof(cuentaContable));
        }

        var id = idPresupuesto.Trim();
        var cuenta = cuentaContable.Trim();
        var companyId = EnsureCompanyId();

        var solicitud = await _context.pst_solicitud_actividad_presupuestos
            .FirstOrDefaultAsync(s =>
                s.solicitud_id == solicitudId &&
                s.company_id == companyId &&
                s.id_presupuesto == id, ct);

        if (solicitud is null)
        {
            throw new KeyNotFoundException("La solicitud no existe.");
        }

        var esCuentaDestino = string.Equals(
            solicitud.cuenta_destino_code?.Trim(),
            cuenta,
            StringComparison.OrdinalIgnoreCase);

        var esCuentaOrigen = string.Equals(
            solicitud.cuenta_origen_code?.Trim(),
            cuenta,
            StringComparison.OrdinalIgnoreCase);

        if (!esCuentaDestino && !esCuentaOrigen)
        {
            throw new InvalidOperationException("La solicitud no pertenece a la cuenta seleccionada.");
        }

        return solicitud;
    }

    private static PresupuestoActividadSolicitudListItemDto MapSolicitud(
        pst_solicitud_actividad_presupuesto solicitud)
    {
        return new PresupuestoActividadSolicitudListItemDto
        {
            SolicitudId = solicitud.solicitud_id,
            IdPresupuesto = solicitud.id_presupuesto,
            TipoActividad = solicitud.tipo_actividad,
            Estado = solicitud.estado,
            CuentaOrigenCode = solicitud.cuenta_origen_code,
            CuentaDestinoCode = solicitud.cuenta_destino_code,
            Monto = solicitud.monto,
            Prioridad = solicitud.prioridad,
            Justificacion = solicitud.justificacion,
            FechaNecesaria = solicitud.fecha_necesaria,
            SolicitadoPor = solicitud.solicitado_por,
            SolicitadoEn = solicitud.solicitado_en,
            RevisadoPor = solicitud.revisado_por,
            RevisadoEn = solicitud.revisado_en,
            ComentarioRevision = solicitud.comentario_revision,
            ActividadId = solicitud.actividad_id
        };
    }

    private static ConfiguracionPresupuestoEditDto MapToEdit(pst_config_presupuesto_hdr header)
    {
        return new ConfiguracionPresupuestoEditDto
        {
            IdPresupuesto = header.id_presupuesto,
            CuentaContable = string.Empty,
            ValorProyeccion = 0m,
            ValorReal = 0m,
            ValorGlobal = header.valor_global,
            ValorDisponible = header.valor_disponible,
            RangoPeriodo = header.rango_periodo,
            FechaInicia = header.fecha_inicia,
            FechaFinaliza = header.fecha_finaliza,
            EstadoAprobado = header.estado_aprobado
        };
    }

    private static ConfiguracionPresupuestoEditDto MapToEdit(PresupuestoConfigQueryRow row)
    {
        return new ConfiguracionPresupuestoEditDto
        {
            IdPresupuesto = row.IdPresupuesto,
            CuentaContable = row.CuentaContableCodigo,
            ValorProyeccion = row.ValorProyeccion,
            ValorReal = row.ValorReal,
            ValorGlobal = row.ValorGlobal,
            ValorDisponible = row.ValorDisponible,
            RangoPeriodo = row.RangoPeriodo,
            FechaInicia = row.FechaInicia,
            FechaFinaliza = row.FechaFinaliza,
            EstadoAprobado = row.EstadoAprobado
        };
    }

    private async Task<IReadOnlyList<string>> GetPresupuestoCuentaCodesAsync(long companyId, CancellationToken ct)
    {
        if (!_context.Database.IsRelational())
        {
            return await _context.con_plan_cuentas
                .AsNoTracking()
                .Where(c => c.company_id == companyId && c.allows_budget)
                .OrderBy(c => c.code)
                .Select(c => c.code)
                .ToListAsync(ct);
        }

        var connection = _context.Database.GetDbConnection();
        var shouldCloseConnection = connection.State != ConnectionState.Open;

        if (shouldCloseConnection)
        {
            await connection.OpenAsync(ct);
        }

        try
        {
            var budgetAmountDataType = await GetColumnDataTypeAsync(connection, "budget_amount", ct);
            var allowsBudgetDataType = await GetColumnDataTypeAsync(connection, "allows_budget", ct);

            var conditions = new List<string>();
            if (!string.IsNullOrWhiteSpace(budgetAmountDataType))
            {
                conditions.Add(BuildTruthyConditionSql("c.budget_amount", budgetAmountDataType));
            }

            if (!string.IsNullOrWhiteSpace(allowsBudgetDataType))
            {
                conditions.Add(BuildTruthyConditionSql("c.allows_budget", allowsBudgetDataType));
            }

            if (conditions.Count == 0)
            {
                return Array.Empty<string>();
            }

            var condition = string.Join(" OR ", conditions);

            await using var command = connection.CreateCommand();
            command.CommandText = $@"
SELECT c.code
  FROM public.con_plan_cuentas c
 WHERE c.company_id = @companyId
   AND {condition}
 ORDER BY c.code;";

            var companyIdParameter = command.CreateParameter();
            companyIdParameter.ParameterName = "@companyId";
            companyIdParameter.DbType = DbType.Int64;
            companyIdParameter.Value = companyId;
            command.Parameters.Add(companyIdParameter);

            var codes = new List<string>();
            await using var reader = await command.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                if (reader.IsDBNull(0))
                {
                    continue;
                }

                var rawCode = reader.GetString(0);
                if (string.IsNullOrWhiteSpace(rawCode))
                {
                    continue;
                }

                codes.Add(rawCode.Trim());
            }

            return codes;
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            return await _context.con_plan_cuentas
                .AsNoTracking()
                .Where(c => c.company_id == companyId && c.allows_budget)
                .OrderBy(c => c.code)
                .Select(c => c.code)
                .ToListAsync(ct);
        }
        finally
        {
            if (shouldCloseConnection)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<string?> GetColumnDataTypeAsync(
        DbConnection connection,
        string columnName,
        CancellationToken ct)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = @"
SELECT data_type
  FROM information_schema.columns
 WHERE table_schema = 'public'
   AND table_name = 'con_plan_cuentas'
   AND column_name = @columnName
 LIMIT 1;";

        var columnParameter = command.CreateParameter();
        columnParameter.ParameterName = "@columnName";
        columnParameter.DbType = DbType.String;
        columnParameter.Value = columnName;
        command.Parameters.Add(columnParameter);

        var rawResult = await command.ExecuteScalarAsync(ct);
        if (rawResult is null || rawResult is DBNull)
        {
            return null;
        }

        var dataType = Convert.ToString(rawResult, CultureInfo.InvariantCulture);
        return string.IsNullOrWhiteSpace(dataType)
            ? null
            : dataType.Trim().ToLowerInvariant();
    }

    private static string BuildTruthyConditionSql(string columnExpression, string dataType)
    {
        return dataType switch
        {
            "boolean" => $"COALESCE({columnExpression}, FALSE)",
            "smallint" or "integer" or "bigint" or "numeric" or "decimal" or "real" or "double precision"
                => $"COALESCE({columnExpression}, 0) <> 0",
            _ => $"UPPER(TRIM(COALESCE({columnExpression}::text, ''))) IN ('1', 'T', 'TRUE', 'Y', 'YES', 'SI', 'S')"
        };
    }

    private sealed class PresupuestoConfigQueryRow
    {
        public string IdPresupuesto { get; init; } = string.Empty;
        public string CuentaContableCodigo { get; init; } = string.Empty;
        public decimal ValorGlobal { get; init; }
        public decimal ValorDisponible { get; init; }
        public decimal ValorProyeccion { get; init; }
        public decimal ValorReal { get; init; }
        public int RangoPeriodo { get; init; }
        public DateOnly FechaInicia { get; init; }
        public DateOnly FechaFinaliza { get; init; }
        public bool EstadoAprobado { get; init; }
    }
}
