using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Retenciones;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Retenciones;

/// <summary>
/// Catálogo de retenciones a proveedores y sus tasas con vigencia, + la cuenta contable del pasivo
/// por empresa. Calcado de <c>ImpuestosService</c>.
/// <para>
/// REGLA CENTRAL: las tasas cambian por decreto y NO se editan en sitio — se cierra la vigencia de
/// la actual y se crea una nueva (<see cref="CambiarTasaAsync"/>). El no-solape lo garantiza la BD
/// (EXCLUDE gist por <c>retencion_id</c>, SQLSTATE 23P01) y se valida además en C# para dar un
/// mensaje humano.
/// </para>
/// </summary>
public sealed class RetencionesService : IRetencionesService
{
    private readonly SiadDbContext _context;

    public RetencionesService(SiadDbContext context)
    {
        _context = context;
    }

    // "Infinito" para comparar rangos abiertos, igual que el COALESCE(..., 'infinity') del EXCLUDE.
    private static readonly DateOnly Infinito = DateOnly.MaxValue;

    // =====================================================================
    // Retención (concepto)
    // =====================================================================

    public async Task<IReadOnlyList<RetencionListItemDto>> GetAsync(RetencionFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new RetencionFilterDto();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.cfg_retenciones.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(r => r.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.TipoImpuesto))
        {
            var tipo = filtro.TipoImpuesto.Trim().ToUpperInvariant();
            query = query.Where(r => r.tipo_impuesto == tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(r => EF.Functions.ILike(r.codigo, like) || EF.Functions.ILike(r.nombre, like))
                : query.Where(r => r.codigo.ToLower().Contains(term.ToLower()) || r.nombre.ToLower().Contains(term.ToLower()));
        }

        // La cuenta se lee de prv_retencion_cuenta, que el filtro global acota a la empresa actual.
        return await query
            .OrderBy(r => r.codigo)
            .Select(r => new RetencionListItemDto
            {
                Id = r.id,
                Codigo = r.codigo,
                Nombre = r.nombre,
                Descripcion = r.descripcion,
                BaseCalculo = r.base_calculo,
                TipoImpuesto = r.tipo_impuesto,
                Activo = r.activo,
                TotalTasas = r.tasas.Count,
                TasasVigentes = r.tasas.Count(t =>
                    t.activo
                    && t.vigencia_desde <= hoy
                    && (t.vigencia_hasta == null || t.vigencia_hasta >= hoy)),
                PorcentajeVigente = r.tasas
                    .Where(t => t.activo
                        && t.vigencia_desde <= hoy
                        && (t.vigencia_hasta == null || t.vigencia_hasta >= hoy))
                    .Select(t => (decimal?)t.porcentaje)
                    .FirstOrDefault(),
                CuentaConfigurada = _context.prv_retencion_cuentas.Any(c => c.retencion_id == r.id && c.activo),
                CuentaCodigo = _context.prv_retencion_cuentas
                    .Where(c => c.retencion_id == r.id && c.activo)
                    .Select(c => c.account!.code)
                    .FirstOrDefault(),
                CuentaNombre = _context.prv_retencion_cuentas
                    .Where(c => c.retencion_id == r.id && c.activo)
                    .Select(c => c.account!.name)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<RetencionEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        return await _context.cfg_retenciones.AsNoTracking()
            .Where(r => r.id == id)
            .Select(r => new RetencionEditDto
            {
                Id = r.id,
                Codigo = r.codigo,
                Nombre = r.nombre,
                Descripcion = r.descripcion,
                BaseCalculo = r.base_calculo,
                TipoImpuesto = r.tipo_impuesto,
                Activo = r.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RetencionDetalleDto?> GetDetalleAsync(int id, CancellationToken ct = default)
    {
        var retencion = await GetByIdAsync(id, ct);
        if (retencion is null) return null;

        var tasas = await GetTasasAsync(id, ct);
        var cuenta = await GetCuentaAsync(id, ct);

        return new RetencionDetalleDto
        {
            Retencion = retencion,
            Tasas = tasas.ToList(),
            Cuenta = cuenta
        };
    }

    public async Task<RetencionEditDto> CreateAsync(RetencionEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = Requerido(dto.Codigo, 20, "código de la retención", mayus: true);
        var nombre = Requerido(dto.Nombre, 80, "nombre de la retención");
        var baseCalc = NormalizarBase(dto.BaseCalculo);
        var tipoImp = NormalizarTipoImpuesto(dto.TipoImpuesto);

        if (await _context.cfg_retenciones.AsNoTracking().AnyAsync(r => r.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe una retención con el código {codigo}.");
        }

        var entity = new cfg_retencion
        {
            codigo = codigo,
            nombre = nombre,
            descripcion = Opcional(dto.Descripcion, 250),
            base_calculo = baseCalc,
            tipo_impuesto = tipoImp,
            activo = dto.Activo,
            usuariocreacion = Usuario(user),
            fechacreacion = Ahora()
        };

        _context.cfg_retenciones.Add(entity);
        await GuardarAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    public async Task<RetencionEditDto> UpdateAsync(int id, RetencionEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.cfg_retenciones.FirstOrDefaultAsync(r => r.id == id, ct)
                     ?? throw new KeyNotFoundException("La retención no existe.");

        var codigo = Requerido(dto.Codigo, 20, "código de la retención", mayus: true);
        var nombre = Requerido(dto.Nombre, 80, "nombre de la retención");
        var baseCalc = NormalizarBase(dto.BaseCalculo);
        var tipoImp = NormalizarTipoImpuesto(dto.TipoImpuesto);

        if (await _context.cfg_retenciones.AsNoTracking().AnyAsync(r => r.codigo == codigo && r.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe una retención con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.descripcion = Opcional(dto.Descripcion, 250);
        entity.base_calculo = baseCalc;
        entity.tipo_impuesto = tipoImp;
        entity.activo = dto.Activo;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await GuardarAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.cfg_retenciones.FirstOrDefaultAsync(r => r.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await GuardarAsync(ct);
        return true;
    }

    // =====================================================================
    // Tasas
    // =====================================================================

    public async Task<IReadOnlyList<RetencionTasaDto>> GetTasasAsync(int retencionId, CancellationToken ct = default)
    {
        if (retencionId <= 0) return Array.Empty<RetencionTasaDto>();

        return await _context.cfg_retencion_tasas.AsNoTracking()
            .Where(t => t.retencion_id == retencionId)
            // Primero la abierta (vigente), luego el histórico más reciente.
            .OrderByDescending(t => t.vigencia_hasta == null)
            .ThenByDescending(t => t.vigencia_desde)
            .Select(t => new RetencionTasaDto
            {
                Id = t.id,
                RetencionId = t.retencion_id,
                Porcentaje = t.porcentaje,
                VigenciaDesde = t.vigencia_desde,
                VigenciaHasta = t.vigencia_hasta,
                Descripcion = t.descripcion,
                Activo = t.activo
            })
            .ToListAsync(ct);
    }

    public async Task<RetencionTasaDto?> GetTasaByIdAsync(int tasaId, CancellationToken ct = default)
    {
        if (tasaId <= 0) return null;

        return await _context.cfg_retencion_tasas.AsNoTracking()
            .Where(t => t.id == tasaId)
            .Select(t => new RetencionTasaDto
            {
                Id = t.id,
                RetencionId = t.retencion_id,
                Porcentaje = t.porcentaje,
                VigenciaDesde = t.vigencia_desde,
                VigenciaHasta = t.vigencia_hasta,
                Descripcion = t.descripcion,
                Activo = t.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RetencionTasaDto> CreateTasaAsync(RetencionTasaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var retencionId = dto.RetencionId;
        if (retencionId <= 0) throw new InvalidOperationException("Debe indicar la retención a la que pertenece la tasa.");

        if (!await _context.cfg_retenciones.AsNoTracking().AnyAsync(r => r.id == retencionId, ct))
        {
            throw new KeyNotFoundException("La retención no existe.");
        }

        var porcentaje = ValidarPorcentaje(dto.Porcentaje);
        ValidarRangoVigencia(dto.VigenciaDesde, dto.VigenciaHasta);

        await ValidarSolapeAsync(retencionId, dto.VigenciaDesde, dto.VigenciaHasta, excluirTasaId: null, ct);

        var entity = new cfg_retencion_tasa
        {
            retencion_id = retencionId,
            porcentaje = porcentaje,
            vigencia_desde = dto.VigenciaDesde,
            vigencia_hasta = dto.VigenciaHasta,
            descripcion = Opcional(dto.Descripcion, 250),
            activo = dto.Activo,
            usuariocreacion = Usuario(user),
            fechacreacion = Ahora()
        };

        _context.cfg_retencion_tasas.Add(entity);
        await GuardarAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    /// <summary>
    /// Edición directa de una tasa. Es para CORREGIR ERRATAS, no para aplicar un decreto: si la
    /// tasa ya se usó, cambiar aquí el porcentaje reescribe el pasado. Para un decreto use
    /// <see cref="CambiarTasaAsync"/>.
    /// </summary>
    public async Task<RetencionTasaDto> UpdateTasaAsync(int tasaId, RetencionTasaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (tasaId <= 0) throw new ArgumentOutOfRangeException(nameof(tasaId));

        var entity = await _context.cfg_retencion_tasas.FirstOrDefaultAsync(t => t.id == tasaId, ct)
                     ?? throw new KeyNotFoundException("La tasa no existe.");

        var porcentaje = ValidarPorcentaje(dto.Porcentaje);
        ValidarRangoVigencia(dto.VigenciaDesde, dto.VigenciaHasta);

        await ValidarSolapeAsync(entity.retencion_id, dto.VigenciaDesde, dto.VigenciaHasta, excluirTasaId: tasaId, ct);

        entity.porcentaje = porcentaje;
        entity.vigencia_desde = dto.VigenciaDesde;
        entity.vigencia_hasta = dto.VigenciaHasta;
        entity.descripcion = Opcional(dto.Descripcion, 250);
        entity.activo = dto.Activo;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await GuardarAsync(ct);

        dto.Id = entity.id;
        dto.RetencionId = entity.retencion_id;
        return dto;
    }

    public async Task<bool> DeactivateTasaAsync(int tasaId, string user, CancellationToken ct = default)
    {
        if (tasaId <= 0) throw new ArgumentOutOfRangeException(nameof(tasaId));

        var entity = await _context.cfg_retencion_tasas.FirstOrDefaultAsync(t => t.id == tasaId, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await GuardarAsync(ct);
        return true;
    }

    /// <summary>
    /// Cambio de tasa por decreto, TRANSACCIONAL: (1) cierra la vigencia de la tasa actual y
    /// (2) crea la nueva de la misma retención, empezando al día siguiente. Si (2) falla, (1) se
    /// revierte — nunca queda una tasa cerrada sin sucesora.
    /// </summary>
    public async Task<RetencionTasaDto> CambiarTasaAsync(CambiarTasaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var actual = await _context.cfg_retencion_tasas.FirstOrDefaultAsync(t => t.id == dto.TasaId, ct)
                     ?? throw new KeyNotFoundException("La tasa que intenta cambiar no existe.");

        if (!actual.activo)
        {
            throw new InvalidOperationException(
                "La tasa está inactiva. Solo se puede cambiar por decreto una tasa activa y vigente.");
        }

        if (actual.vigencia_hasta is not null)
        {
            throw new InvalidOperationException(
                $"Esa tasa ya está cerrada (vigente hasta {actual.vigencia_hasta:dd/MM/yyyy}). " +
                "El cambio por decreto solo aplica sobre la tasa vigente.");
        }

        if (dto.VigenciaHasta < actual.vigencia_desde)
        {
            throw new InvalidOperationException(
                $"La fecha de cierre no puede ser anterior al inicio de vigencia de la tasa actual " +
                $"({actual.vigencia_desde:dd/MM/yyyy}).");
        }

        var nuevoPorcentaje = ValidarPorcentaje(dto.NuevoPorcentaje);
        var nuevaDesde = dto.VigenciaHasta.AddDays(1);

        // Chequeo amable antes de que el EXCLUDE reviente: ¿hay otra tasa de la misma retención
        // pisando el rango que va a ocupar la nueva?
        await ValidarSolapeAsync(actual.retencion_id, nuevaDesde, null, excluirTasaId: actual.id, ct);

        var nueva = new cfg_retencion_tasa
        {
            retencion_id = actual.retencion_id,
            porcentaje = nuevoPorcentaje,
            vigencia_desde = nuevaDesde,
            vigencia_hasta = null,
            descripcion = string.IsNullOrWhiteSpace(dto.NuevaDescripcion)
                ? actual.descripcion
                : Opcional(dto.NuevaDescripcion, 250),
            activo = true,
            usuariocreacion = Usuario(user),
            fechacreacion = Ahora()
        };

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // (1) cerrar la vigente
            actual.vigencia_hasta = dto.VigenciaHasta;
            actual.usuariomodificacion = Usuario(user);
            actual.fechamodificacion = Ahora();
            await _context.SaveChangesAsync(ct);

            // (2) crear la sucesora
            _context.cfg_retencion_tasas.Add(nueva);
            await _context.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync(ct);
            _context.ChangeTracker.Clear();
            throw TraducirErrorDeBd(ex);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            _context.ChangeTracker.Clear();
            throw;
        }

        return MapTasa(nueva);
    }

    public async Task<IReadOnlyList<RetencionTasaLookupDto>> GetTasasVigentesAsync(DateOnly fecha, CancellationToken ct = default)
    {
        return await _context.cfg_retencion_tasas.AsNoTracking()
            .Where(t => t.activo
                && t.retencion!.activo
                && t.vigencia_desde <= fecha
                && (t.vigencia_hasta == null || t.vigencia_hasta >= fecha))
            .OrderBy(t => t.retencion!.codigo)
            .Select(t => new RetencionTasaLookupDto
            {
                RetencionId = t.retencion_id,
                Codigo = t.retencion!.codigo,
                Nombre = t.retencion!.nombre,
                BaseCalculo = t.retencion!.base_calculo,
                TipoImpuesto = t.retencion!.tipo_impuesto,
                TasaId = t.id,
                Porcentaje = t.porcentaje,
                VigenciaDesde = t.vigencia_desde,
                VigenciaHasta = t.vigencia_hasta
            })
            .ToListAsync(ct);
    }

    // Códigos de la semilla de impuestos (Database/2026-07-14_cfg_impuestos.sql): el ISV general
    // (cfg_impuesto.codigo='ISV', tasa cfg_impuesto_tasa.codigo='ISV15', GRAVADO 15%). La base SIN_ISV
    // de una retención divide el bruto entre (1 + ISV/100) para quitarle ese ISV. Si no hubiera tasa
    // vigente, GetAplicablesAsync devuelve null y la UI avisa que la base no se redujo.
    private const string ImpuestoIsvCodigo = "ISV";
    private const string ImpuestoTasaIsvGeneralCodigo = "ISV15";

    public async Task<RetencionesAplicablesDto> GetAplicablesAsync(DateOnly fecha, CancellationToken ct = default)
    {
        // Retenciones ACTIVAS con: % vigente a la fecha (LEFT: null si no hay tasa) y la cuenta del
        // pasivo configurada en la empresa actual (LEFT: null si no configurada). Incluye a propósito
        // las que NO tienen tasa vigente (p. ej. ISV-RET) para que F2 las muestre y avise "sin tasa".
        // prv_retencion_cuentas es ICompanyScopedEntity: el filtro global lo acota a la empresa actual.
        var retenciones = await _context.cfg_retenciones.AsNoTracking()
            .Where(r => r.activo)
            .OrderBy(r => r.codigo)
            .Select(r => new RetencionAplicableDto
            {
                RetencionId = r.id,
                Codigo = r.codigo,
                Nombre = r.nombre,
                BaseCalculo = r.base_calculo,
                TipoImpuesto = r.tipo_impuesto,
                Porcentaje = r.tasas
                    .Where(t => t.activo
                        && t.vigencia_desde <= fecha
                        && (t.vigencia_hasta == null || t.vigencia_hasta >= fecha))
                    .Select(t => (decimal?)t.porcentaje)
                    .FirstOrDefault(),
                TasaId = r.tasas
                    .Where(t => t.activo
                        && t.vigencia_desde <= fecha
                        && (t.vigencia_hasta == null || t.vigencia_hasta >= fecha))
                    .Select(t => (int?)t.id)
                    .FirstOrDefault(),
                CuentaAccountId = _context.prv_retencion_cuentas
                    .Where(c => c.retencion_id == r.id && c.activo)
                    .Select(c => (long?)c.account_id)
                    .FirstOrDefault(),
                CuentaCodigo = _context.prv_retencion_cuentas
                    .Where(c => c.retencion_id == r.id && c.activo)
                    .Select(c => c.account!.code)
                    .FirstOrDefault(),
                CuentaNombre = _context.prv_retencion_cuentas
                    .Where(c => c.retencion_id == r.id && c.activo)
                    .Select(c => c.account!.name)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);

        return new RetencionesAplicablesDto
        {
            TasaIsvGeneral = await ResolverTasaIsvGeneralAsync(fecha, ct),
            Retenciones = retenciones
        };
    }

    /// <summary>
    /// % de la tasa ISV general (GRAVADO) vigente a la fecha, para la base SIN_ISV. null si no hay.
    /// </summary>
    private async Task<decimal?> ResolverTasaIsvGeneralAsync(DateOnly fecha, CancellationToken ct)
    {
        return await _context.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.impuesto!.codigo == ImpuestoIsvCodigo
                && t.codigo == ImpuestoTasaIsvGeneralCodigo
                && t.tipo == TipoImpuestoTasa.Gravado
                && t.activo
                && t.impuesto!.activo
                && t.vigencia_desde <= fecha
                && (t.vigencia_hasta == null || t.vigencia_hasta >= fecha))
            .Select(t => (decimal?)t.porcentaje)
            .FirstOrDefaultAsync(ct);
    }

    // =====================================================================
    // Cuenta del pasivo por empresa (tenant-scoped)
    // =====================================================================

    public async Task<IReadOnlyList<CuentaPosteableDto>> GetCuentasPosteablesAsync(CancellationToken ct = default)
    {
        // con_plan_cuentas es ICompanyScopedEntity: el filtro global lo acota a la empresa actual.
        return await _context.con_plan_cuentas.AsNoTracking()
            .Where(c => c.allows_posting)
            .OrderBy(c => c.code)
            .Select(c => new CuentaPosteableDto
            {
                AccountId = c.account_id,
                Codigo = c.code,
                Nombre = c.name
            })
            .ToListAsync(ct);
    }

    public async Task<RetencionCuentaDto?> GetCuentaAsync(int retencionId, CancellationToken ct = default)
    {
        if (retencionId <= 0) return null;

        // prv_retencion_cuentas: filtro global por empresa actual. UNIQUE (company_id, retencion_id).
        return await _context.prv_retencion_cuentas.AsNoTracking()
            .Where(c => c.retencion_id == retencionId)
            .Select(c => new RetencionCuentaDto
            {
                RetencionId = c.retencion_id,
                AccountId = c.account_id,
                Activo = c.activo,
                RetencionCodigo = c.retencion!.codigo,
                CuentaCodigo = c.account!.code,
                CuentaNombre = c.account!.name
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<RetencionCuentaDto> SetCuentaAsync(int retencionId, RetencionCuentaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (retencionId <= 0) throw new ArgumentOutOfRangeException(nameof(retencionId));

        if (!await _context.cfg_retenciones.AsNoTracking().AnyAsync(r => r.id == retencionId, ct))
        {
            throw new KeyNotFoundException("La retención no existe.");
        }

        if (dto.AccountId <= 0)
        {
            throw new InvalidOperationException("Debe seleccionar una cuenta contable.");
        }

        // La cuenta debe ser posteable y de la empresa actual (el filtro global de con_plan_cuentas
        // ya la acota a la empresa: si no aparece, no es de esta empresa).
        var cuentaValida = await _context.con_plan_cuentas.AsNoTracking()
            .AnyAsync(c => c.account_id == dto.AccountId && c.allows_posting, ct);
        if (!cuentaValida)
        {
            throw new InvalidOperationException(
                "La cuenta seleccionada no existe, no pertenece a esta empresa o no es una cuenta posteable.");
        }

        var existente = await _context.prv_retencion_cuentas.FirstOrDefaultAsync(c => c.retencion_id == retencionId, ct);
        if (existente is null)
        {
            var nueva = new prv_retencion_cuenta
            {
                retencion_id = retencionId,
                account_id = dto.AccountId,
                activo = dto.Activo,
                usuariocreacion = Usuario(user),
                fechacreacion = Ahora()
                // company_id lo estampa SaveChanges (SiadDbContext.Tenancy.cs).
            };
            _context.prv_retencion_cuentas.Add(nueva);
        }
        else
        {
            existente.account_id = dto.AccountId;
            existente.activo = dto.Activo;
            existente.usuariomodificacion = Usuario(user);
            existente.fechamodificacion = Ahora();
        }

        await GuardarAsync(ct);

        return (await GetCuentaAsync(retencionId, ct))!;
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static RetencionTasaDto MapTasa(cfg_retencion_tasa t) => new()
    {
        Id = t.id,
        RetencionId = t.retencion_id,
        Porcentaje = t.porcentaje,
        VigenciaDesde = t.vigencia_desde,
        VigenciaHasta = t.vigencia_hasta,
        Descripcion = t.descripcion,
        Activo = t.activo
    };

    private static string NormalizarBase(string? baseCalculo)
    {
        var b = (baseCalculo ?? string.Empty).Trim().ToUpperInvariant();
        if (!BaseRetencion.EsValido(b))
        {
            throw new InvalidOperationException("La base de cálculo debe ser TOTAL o SIN_ISV.");
        }
        return b;
    }

    private static string NormalizarTipoImpuesto(string? tipoImpuesto)
    {
        var t = (tipoImpuesto ?? string.Empty).Trim().ToUpperInvariant();
        if (!TipoImpuestoRetencion.EsValido(t))
        {
            throw new InvalidOperationException("El tipo de impuesto debe ser ISR o ISV.");
        }
        return t;
    }

    /// <summary>Espejo en C# de <c>ck_cfg_retencion_tasa_rango</c>: una retención siempre retiene algo.</summary>
    private static decimal ValidarPorcentaje(decimal porcentaje)
    {
        if (porcentaje <= 0 || porcentaje > 100)
        {
            throw new InvalidOperationException("El porcentaje debe ser mayor que 0 y hasta 100.");
        }
        return porcentaje;
    }

    /// <summary>Espejo en C# de <c>ck_cfg_retencion_tasa_vigencia</c>.</summary>
    private static void ValidarRangoVigencia(DateOnly desde, DateOnly? hasta)
    {
        if (hasta is not null && hasta < desde)
        {
            throw new InvalidOperationException(
                "La fecha 'vigente hasta' no puede ser anterior a 'vigente desde'.");
        }
    }

    /// <summary>
    /// Espejo en C# del EXCLUDE <c>ex_cfg_retencion_tasa_vigencia</c>: dos tasas de la misma
    /// retención no pueden tener vigencias solapadas. Se valida aquí para dar un mensaje humano; la
    /// BD sigue siendo la red de seguridad ante carreras (SQLSTATE 23P01).
    /// </summary>
    private async Task ValidarSolapeAsync(
        int retencionId, DateOnly desde, DateOnly? hasta, int? excluirTasaId, CancellationToken ct)
    {
        var query = _context.cfg_retencion_tasas.AsNoTracking()
            .Where(t => t.retencion_id == retencionId);

        if (excluirTasaId.HasValue)
        {
            var excluir = excluirTasaId.Value;
            query = query.Where(t => t.id != excluir);
        }

        var candidatas = await query
            .Select(t => new { t.vigencia_desde, t.vigencia_hasta })
            .ToListAsync(ct);

        var nuevoHasta = hasta ?? Infinito;

        foreach (var c in candidatas)
        {
            var cHasta = c.vigencia_hasta ?? Infinito;

            // Rangos cerrados [desde, hasta]: se solapan si cada uno empieza antes de que el otro termine.
            var seSolapan = desde <= cHasta && c.vigencia_desde <= nuevoHasta;
            if (!seSolapan) continue;

            var textoHasta = c.vigencia_hasta is null
                ? "(vigente)"
                : c.vigencia_hasta.Value.ToString("dd/MM/yyyy");

            throw new InvalidOperationException(
                $"Ya existe una tasa vigente del {c.vigencia_desde:dd/MM/yyyy} al {textoHasta} para esta retención, " +
                "y su vigencia se solapa con la que intenta registrar. Dos tasas de la misma retención no pueden " +
                "regir a la vez. Si la tasa cambió por decreto, use \"Cambiar tasa (nuevo decreto)\": eso cierra la " +
                "vigente y abre la nueva al día siguiente.");
        }
    }

    private async Task GuardarAsync(CancellationToken ct)
    {
        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex)
        {
            throw TraducirErrorDeBd(ex);
        }
    }

    /// <summary>
    /// Traduce los errores de Postgres a mensajes humanos. Sin esto, un solape de vigencias le
    /// llega al usuario como un 500 crudo con el texto de una constraint.
    /// </summary>
    private static Exception TraducirErrorDeBd(DbUpdateException ex)
    {
        if (ex.GetBaseException() is not PostgresException pg)
        {
            return ex;
        }

        var constraint = pg.ConstraintName ?? string.Empty;

        return pg.SqlState switch
        {
            // 23P01 exclusion_violation -> ex_cfg_retencion_tasa_vigencia
            "23P01" => new InvalidOperationException(
                "Ya existe una tasa de esta retención cuya vigencia se solapa con la que intenta guardar. " +
                "Dos tasas de la misma retención no pueden regir al mismo tiempo. Si la tasa cambió por decreto, " +
                "use \"Cambiar tasa (nuevo decreto)\".", ex),

            // 23514 check_violation
            "23514" when constraint.Contains("base", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException("La base de cálculo debe ser TOTAL o SIN_ISV.", ex),

            "23514" when constraint.Contains("tipo_impuesto", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException("El tipo de impuesto debe ser ISR o ISV.", ex),

            "23514" when constraint.Contains("rango", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException("El porcentaje debe ser mayor que 0 y hasta 100.", ex),

            "23514" when constraint.Contains("vigencia", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException("La fecha 'vigente hasta' no puede ser anterior a 'vigente desde'.", ex),

            "23514" => new InvalidOperationException(
                "Los datos no cumplen una validación de la base de datos.", ex),

            // 23505 unique_violation -> uq_cfg_retencion_codigo o uq_prv_retencion_cuenta_company_retencion
            "23505" when constraint.Contains("cuenta", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException("Esta retención ya tiene una cuenta configurada en la empresa.", ex),

            "23505" => new InvalidOperationException("Ya existe una retención con ese código.", ex),

            // 23503 foreign_key_violation -> ON DELETE RESTRICT / FK a cuenta o retención
            "23503" => new InvalidOperationException(
                "No se puede completar la operación por una dependencia: la retención tiene tasas o una cuenta " +
                "asociada, o la cuenta indicada no existe. Desactive en lugar de eliminar.", ex),

            _ => ex
        };
    }

    private static string Requerido(string? valor, int max, string campo, bool mayus = false)
    {
        var v = (valor ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v))
        {
            throw new InvalidOperationException($"El {campo} es obligatorio.");
        }
        if (v.Length > max)
        {
            throw new InvalidOperationException($"El {campo} no puede superar los {max} caracteres.");
        }
        return mayus ? v.ToUpperInvariant() : v;
    }

    private static string? Opcional(string? valor, int max)
    {
        var v = (valor ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(v)) return null;
        return v.Length > max ? v[..max] : v;
    }

    private static string Usuario(string? user)
    {
        var u = (user ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(u)) return "system";
        return u.Length > 100 ? u[..100] : u;
    }

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
