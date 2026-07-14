using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Impuestos;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Impuestos;

/// <summary>
/// Catálogo global de impuestos y tasas con vigencia.
/// <para>
/// REGLA CENTRAL: las tasas cambian por decreto y NO se editan en sitio — se cierra la
/// vigencia de la actual y se crea una nueva (<see cref="CambiarTasaAsync"/>). Editar
/// en sitio haría que reimprimir una factura vieja diera el impuesto de hoy.
/// </para>
/// </summary>
public sealed class ImpuestosService : IImpuestosService
{
    private readonly SiadDbContext _context;

    public ImpuestosService(SiadDbContext context)
    {
        _context = context;
    }

    // "Infinito" para comparar rangos abiertos, igual que el COALESCE(..., 'infinity')
    // del EXCLUDE de la BD.
    private static readonly DateOnly Infinito = DateOnly.MaxValue;

    // =====================================================================
    // Impuesto
    // =====================================================================

    public async Task<IReadOnlyList<ImpuestoListItemDto>> GetAsync(ImpuestoFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ImpuestoFilterDto();
        var hoy = DateOnly.FromDateTime(DateTime.Today);

        var query = _context.cfg_impuestos.AsNoTracking().AsQueryable();

        if (filtro.Activo.HasValue)
        {
            query = query.Where(i => i.activo == filtro.Activo.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var like = $"%{term}%";
            query = _context.Database.IsRelational()
                ? query.Where(i => EF.Functions.ILike(i.codigo, like) || EF.Functions.ILike(i.nombre, like))
                : query.Where(i => i.codigo.ToLower().Contains(term.ToLower()) || i.nombre.ToLower().Contains(term.ToLower()));
        }

        return await query
            .OrderBy(i => i.codigo)
            .Select(i => new ImpuestoListItemDto
            {
                Id = i.id,
                Codigo = i.codigo,
                Nombre = i.nombre,
                Descripcion = i.descripcion,
                Activo = i.activo,
                TotalTasas = i.tasas.Count,
                TasasVigentes = i.tasas.Count(t =>
                    t.activo
                    && t.vigencia_desde <= hoy
                    && (t.vigencia_hasta == null || t.vigencia_hasta >= hoy))
            })
            .ToListAsync(ct);
    }

    public async Task<ImpuestoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        return await _context.cfg_impuestos.AsNoTracking()
            .Where(i => i.id == id)
            .Select(i => new ImpuestoEditDto
            {
                Id = i.id,
                Codigo = i.codigo,
                Nombre = i.nombre,
                Descripcion = i.descripcion,
                Activo = i.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ImpuestoDetalleDto?> GetDetalleAsync(int id, CancellationToken ct = default)
    {
        var impuesto = await GetByIdAsync(id, ct);
        if (impuesto is null) return null;

        var tasas = await GetTasasAsync(id, ct);
        return new ImpuestoDetalleDto
        {
            Impuesto = impuesto,
            Tasas = tasas.ToList()
        };
    }

    public async Task<ImpuestoEditDto> CreateAsync(ImpuestoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = Requerido(dto.Codigo, 10, "código del impuesto", mayus: true);
        var nombre = Requerido(dto.Nombre, 80, "nombre del impuesto");

        if (await _context.cfg_impuestos.AsNoTracking().AnyAsync(i => i.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe un impuesto con el código {codigo}.");
        }

        var entity = new cfg_impuesto
        {
            codigo = codigo,
            nombre = nombre,
            descripcion = Opcional(dto.Descripcion, 250),
            activo = dto.Activo,
            usuariocreacion = Usuario(user),
            fechacreacion = Ahora()
        };

        _context.cfg_impuestos.Add(entity);
        await GuardarAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    public async Task<ImpuestoEditDto> UpdateAsync(int id, ImpuestoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.cfg_impuestos.FirstOrDefaultAsync(i => i.id == id, ct)
                     ?? throw new KeyNotFoundException("El impuesto no existe.");

        var codigo = Requerido(dto.Codigo, 10, "código del impuesto", mayus: true);
        var nombre = Requerido(dto.Nombre, 80, "nombre del impuesto");

        if (await _context.cfg_impuestos.AsNoTracking().AnyAsync(i => i.codigo == codigo && i.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un impuesto con el código {codigo}.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.descripcion = Opcional(dto.Descripcion, 250);
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

        var entity = await _context.cfg_impuestos.FirstOrDefaultAsync(i => i.id == id, ct);
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

    public async Task<IReadOnlyList<ImpuestoTasaDto>> GetTasasAsync(int impuestoId, CancellationToken ct = default)
    {
        if (impuestoId <= 0) return Array.Empty<ImpuestoTasaDto>();

        return await _context.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.impuesto_id == impuestoId)
            // Primero las abiertas (vigentes), luego el histórico más reciente.
            .OrderByDescending(t => t.vigencia_hasta == null)
            .ThenByDescending(t => t.vigencia_desde)
            .ThenBy(t => t.codigo)
            .Select(t => new ImpuestoTasaDto
            {
                Id = t.id,
                ImpuestoId = t.impuesto_id,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Tipo = t.tipo,
                Porcentaje = t.porcentaje,
                VigenciaDesde = t.vigencia_desde,
                VigenciaHasta = t.vigencia_hasta,
                Descripcion = t.descripcion,
                Activo = t.activo
            })
            .ToListAsync(ct);
    }

    public async Task<ImpuestoTasaDto?> GetTasaByIdAsync(int tasaId, CancellationToken ct = default)
    {
        if (tasaId <= 0) return null;

        return await _context.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.id == tasaId)
            .Select(t => new ImpuestoTasaDto
            {
                Id = t.id,
                ImpuestoId = t.impuesto_id,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Tipo = t.tipo,
                Porcentaje = t.porcentaje,
                VigenciaDesde = t.vigencia_desde,
                VigenciaHasta = t.vigencia_hasta,
                Descripcion = t.descripcion,
                Activo = t.activo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ImpuestoTasaDto> CreateTasaAsync(ImpuestoTasaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var impuestoId = dto.ImpuestoId;
        if (impuestoId <= 0) throw new InvalidOperationException("Debe indicar el impuesto al que pertenece la tasa.");

        if (!await _context.cfg_impuestos.AsNoTracking().AnyAsync(i => i.id == impuestoId, ct))
        {
            throw new KeyNotFoundException("El impuesto no existe.");
        }

        var codigo = Requerido(dto.Codigo, 20, "código de la tasa", mayus: true);
        var nombre = Requerido(dto.Nombre, 80, "nombre de la tasa");
        var tipo = NormalizarTipo(dto.Tipo);
        var porcentaje = ValidarCoherencia(tipo, dto.Porcentaje);
        ValidarRangoVigencia(dto.VigenciaDesde, dto.VigenciaHasta);

        await ValidarSolapeAsync(impuestoId, codigo, dto.VigenciaDesde, dto.VigenciaHasta, excluirTasaId: null, ct);

        var entity = new cfg_impuesto_tasa
        {
            impuesto_id = impuestoId,
            codigo = codigo,
            nombre = nombre,
            tipo = tipo,
            porcentaje = porcentaje,
            vigencia_desde = dto.VigenciaDesde,
            vigencia_hasta = dto.VigenciaHasta,
            descripcion = Opcional(dto.Descripcion, 250),
            activo = dto.Activo,
            usuariocreacion = Usuario(user),
            fechacreacion = Ahora()
        };

        _context.cfg_impuesto_tasas.Add(entity);
        await GuardarAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    /// <summary>
    /// Edición directa de una tasa. Es para CORREGIR ERRATAS (un nombre mal escrito),
    /// no para aplicar un decreto: si la tasa ya se usó en documentos, cambiar aquí el
    /// porcentaje reescribe el pasado. Para un decreto use <see cref="CambiarTasaAsync"/>.
    /// </summary>
    public async Task<ImpuestoTasaDto> UpdateTasaAsync(int tasaId, ImpuestoTasaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (tasaId <= 0) throw new ArgumentOutOfRangeException(nameof(tasaId));

        var entity = await _context.cfg_impuesto_tasas.FirstOrDefaultAsync(t => t.id == tasaId, ct)
                     ?? throw new KeyNotFoundException("La tasa no existe.");

        var codigo = Requerido(dto.Codigo, 20, "código de la tasa", mayus: true);
        var nombre = Requerido(dto.Nombre, 80, "nombre de la tasa");
        var tipo = NormalizarTipo(dto.Tipo);
        var porcentaje = ValidarCoherencia(tipo, dto.Porcentaje);
        ValidarRangoVigencia(dto.VigenciaDesde, dto.VigenciaHasta);

        await ValidarSolapeAsync(entity.impuesto_id, codigo, dto.VigenciaDesde, dto.VigenciaHasta, excluirTasaId: tasaId, ct);

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.tipo = tipo;
        entity.porcentaje = porcentaje;
        entity.vigencia_desde = dto.VigenciaDesde;
        entity.vigencia_hasta = dto.VigenciaHasta;
        entity.descripcion = Opcional(dto.Descripcion, 250);
        entity.activo = dto.Activo;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await GuardarAsync(ct);

        dto.Id = entity.id;
        dto.ImpuestoId = entity.impuesto_id;
        return dto;
    }

    public async Task<bool> DeactivateTasaAsync(int tasaId, string user, CancellationToken ct = default)
    {
        if (tasaId <= 0) throw new ArgumentOutOfRangeException(nameof(tasaId));

        var entity = await _context.cfg_impuesto_tasas.FirstOrDefaultAsync(t => t.id == tasaId, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = Usuario(user);
        entity.fechamodificacion = Ahora();

        await GuardarAsync(ct);
        return true;
    }

    /// <summary>
    /// Cambio de tasa por decreto, TRANSACCIONAL:
    /// (1) cierra la vigencia de la tasa actual y (2) crea la nueva con el mismo código
    /// y tipo, empezando al día siguiente. Si (2) falla, (1) se revierte — nunca queda
    /// una tasa cerrada sin sucesora, que dejaría al sistema sin tasa aplicable.
    /// </summary>
    public async Task<ImpuestoTasaDto> CambiarTasaAsync(CambiarTasaDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var actual = await _context.cfg_impuesto_tasas.FirstOrDefaultAsync(t => t.id == dto.TasaId, ct)
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

        // El tipo se hereda: un decreto cambia el porcentaje, no la naturaleza fiscal.
        var tipo = actual.tipo;
        var nuevoPorcentaje = ValidarCoherencia(tipo, dto.NuevoPorcentaje);

        var nuevaDesde = dto.VigenciaHasta.AddDays(1);

        // Chequeo amable antes de que el EXCLUDE de Postgres reviente: ¿ya hay otra tasa
        // del mismo código pisando el rango que va a ocupar la nueva?
        await ValidarSolapeAsync(actual.impuesto_id, actual.codigo, nuevaDesde, null,
            excluirTasaId: actual.id, ct);

        var nueva = new cfg_impuesto_tasa
        {
            impuesto_id = actual.impuesto_id,
            codigo = actual.codigo,
            nombre = string.IsNullOrWhiteSpace(dto.NuevoNombre) ? actual.nombre : Opcional(dto.NuevoNombre, 80)!,
            tipo = tipo,
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
            _context.cfg_impuesto_tasas.Add(nueva);
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

    public async Task<IReadOnlyList<ImpuestoTasaLookupDto>> GetTasasVigentesAsync(DateOnly fecha, CancellationToken ct = default)
    {
        return await _context.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.activo
                && t.impuesto!.activo
                && t.vigencia_desde <= fecha
                && (t.vigencia_hasta == null || t.vigencia_hasta >= fecha))
            .OrderBy(t => t.impuesto!.codigo)
            .ThenBy(t => t.codigo)
            .Select(t => new ImpuestoTasaLookupDto
            {
                Id = t.id,
                ImpuestoId = t.impuesto_id,
                ImpuestoCodigo = t.impuesto!.codigo,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Tipo = t.tipo,
                Porcentaje = t.porcentaje,
                VigenciaDesde = t.vigencia_desde,
                VigenciaHasta = t.vigencia_hasta
            })
            .ToListAsync(ct);
    }

    // =====================================================================
    // Helpers
    // =====================================================================

    private static ImpuestoTasaDto MapTasa(cfg_impuesto_tasa t) => new()
    {
        Id = t.id,
        ImpuestoId = t.impuesto_id,
        Codigo = t.codigo,
        Nombre = t.nombre,
        Tipo = t.tipo,
        Porcentaje = t.porcentaje,
        VigenciaDesde = t.vigencia_desde,
        VigenciaHasta = t.vigencia_hasta,
        Descripcion = t.descripcion,
        Activo = t.activo
    };

    private static string NormalizarTipo(string? tipo)
    {
        var t = (tipo ?? string.Empty).Trim().ToUpperInvariant();
        if (!TipoImpuestoTasa.EsValido(t))
        {
            throw new InvalidOperationException("El tipo de tasa debe ser GRAVADO, EXENTO o EXONERADO.");
        }
        return t;
    }

    /// <summary>Espejo en C# de <c>ck_cfg_impuesto_tasa_coherencia</c>.</summary>
    private static decimal ValidarCoherencia(string tipo, decimal porcentaje)
    {
        if (porcentaje < 0 || porcentaje > 100)
        {
            throw new InvalidOperationException("El porcentaje debe estar entre 0 y 100.");
        }

        if (TipoImpuestoTasa.ExigePorcentaje(tipo))
        {
            if (porcentaje <= 0)
            {
                throw new InvalidOperationException(
                    "Una tasa GRAVADA debe tener un porcentaje mayor que cero. " +
                    "Si el renglón no paga impuesto, use el tipo EXENTO o EXONERADO.");
            }
            return porcentaje;
        }

        if (porcentaje != 0)
        {
            throw new InvalidOperationException(
                $"Una tasa {tipo} debe tener porcentaje 0: por definición no paga impuesto.");
        }

        return 0m;
    }

    /// <summary>Espejo en C# de <c>ck_cfg_impuesto_tasa_vigencia</c>.</summary>
    private static void ValidarRangoVigencia(DateOnly desde, DateOnly? hasta)
    {
        if (hasta is not null && hasta < desde)
        {
            throw new InvalidOperationException(
                "La fecha 'vigente hasta' no puede ser anterior a 'vigente desde'.");
        }
    }

    /// <summary>
    /// Espejo en C# del EXCLUDE <c>ex_cfg_impuesto_tasa_vigencia</c>: dos tasas del mismo
    /// código no pueden tener vigencias solapadas (si no, "¿cuál aplica el 3 de marzo?"
    /// no tiene respuesta). Se valida aquí para dar un mensaje humano; la BD sigue siendo
    /// la red de seguridad ante carreras (SQLSTATE 23P01, ver <see cref="TraducirErrorDeBd"/>).
    /// </summary>
    private async Task ValidarSolapeAsync(
        int impuestoId, string codigo, DateOnly desde, DateOnly? hasta, int? excluirTasaId, CancellationToken ct)
    {
        var query = _context.cfg_impuesto_tasas.AsNoTracking()
            .Where(t => t.impuesto_id == impuestoId && t.codigo == codigo);

        if (excluirTasaId.HasValue)
        {
            var excluir = excluirTasaId.Value;
            query = query.Where(t => t.id != excluir);
        }

        var candidatas = await query
            .Select(t => new { t.id, t.vigencia_desde, t.vigencia_hasta })
            .ToListAsync(ct);

        var nuevoHasta = hasta ?? Infinito;

        foreach (var c in candidatas)
        {
            var cHasta = c.vigencia_hasta ?? Infinito;

            // Rangos cerrados [desde, hasta]: se solapan si cada uno empieza antes de que
            // el otro termine.
            var seSolapan = desde <= cHasta && c.vigencia_desde <= nuevoHasta;
            if (!seSolapan) continue;

            var textoHasta = c.vigencia_hasta is null
                ? "(vigente)"
                : c.vigencia_hasta.Value.ToString("dd/MM/yyyy");

            throw new InvalidOperationException(
                $"Ya existe una tasa con el código {codigo} vigente del " +
                $"{c.vigencia_desde:dd/MM/yyyy} al {textoHasta}, y su vigencia se solapa con la que intenta " +
                "registrar. Dos tasas del mismo código no pueden regir a la vez. " +
                "Si la tasa cambió por decreto, use \"Cambiar tasa (nuevo decreto)\": eso cierra la vigente " +
                "y abre la nueva al día siguiente.");
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
    /// Traduce los errores de Postgres a mensajes humanos. Sin esto, un solape de vigencias
    /// le llega al usuario como un 500 crudo con el texto de una constraint.
    /// </summary>
    private static Exception TraducirErrorDeBd(DbUpdateException ex)
    {
        if (ex.GetBaseException() is not PostgresException pg)
        {
            return ex;
        }

        return pg.SqlState switch
        {
            // 23P01 exclusion_violation -> ex_cfg_impuesto_tasa_vigencia
            "23P01" => new InvalidOperationException(
                "Ya existe una tasa con ese código cuya vigencia se solapa con la que intenta guardar. " +
                "Dos tasas del mismo código no pueden regir al mismo tiempo. " +
                "Si la tasa cambió por decreto, use \"Cambiar tasa (nuevo decreto)\": eso cierra la vigencia " +
                "de la tasa actual y abre la nueva al día siguiente, dejando el histórico intacto.", ex),

            // 23514 check_violation -> coherencia tipo/porcentaje o rango de vigencia
            "23514" when (pg.ConstraintName ?? string.Empty).Contains("coherencia", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException(
                    "El porcentaje no es coherente con el tipo: GRAVADO exige porcentaje mayor que cero; " +
                    "EXENTO y EXONERADO exigen porcentaje 0.", ex),

            "23514" when (pg.ConstraintName ?? string.Empty).Contains("vigencia", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException(
                    "La fecha 'vigente hasta' no puede ser anterior a 'vigente desde'.", ex),

            "23514" when (pg.ConstraintName ?? string.Empty).Contains("tipo", StringComparison.OrdinalIgnoreCase) =>
                new InvalidOperationException(
                    "El tipo de tasa debe ser GRAVADO, EXENTO o EXONERADO.", ex),

            "23514" => new InvalidOperationException(
                "Los datos de la tasa no cumplen una validación de la base de datos.", ex),

            // 23505 unique_violation -> uq_cfg_impuesto_codigo
            "23505" => new InvalidOperationException(
                "Ya existe un impuesto con ese código.", ex),

            // 23503 foreign_key_violation -> ON DELETE RESTRICT
            "23503" => new InvalidOperationException(
                "No se puede completar la operación porque el impuesto tiene tasas asociadas. " +
                "Desactívelo en lugar de eliminarlo.", ex),

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
