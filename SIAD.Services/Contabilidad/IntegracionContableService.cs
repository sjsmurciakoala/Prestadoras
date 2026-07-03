using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Implementación de la configuración de integración contable-comercial
/// (plan 2026-07-02 F2). El posteo NO ocurre aquí: esta clase solo administra
/// la configuración que consumirán fn_con_resolver_cuenta y los flujos F3+.
/// </summary>
public sealed class IntegracionContableService : IIntegracionContableService
{
    private readonly SiadDbContext _context;

    public IntegracionContableService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IntegracionContableDto> ObtenerAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);

        var config = await _context.con_integracion_configs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        var cuentas = await (
                from ic in _context.con_integracion_cuentas.AsNoTracking()
                join pc in _context.con_plan_cuentas.AsNoTracking() on ic.account_id equals pc.account_id
                join cs in _context.categoria_servicios.AsNoTracking()
                    on ic.categoria_servicio_id equals (int?)cs.categoria_servicio_id into csj
                from cs in csj.DefaultIfEmpty()
                where ic.company_id == companyId
                orderby ic.uso, ic.servicio_id, ic.categoria_servicio_id, ic.con_medicion
                select new IntegracionCuentaDto
                {
                    IntegracionCuentaId = ic.integracion_cuenta_id,
                    Uso = ic.uso,
                    ServicioId = ic.servicio_id,
                    CategoriaServicioId = ic.categoria_servicio_id,
                    ConMedicion = ic.con_medicion,
                    AccountId = ic.account_id,
                    IsActive = ic.is_active,
                    AccountCode = pc.code,
                    AccountName = pc.name,
                    CategoriaDescripcion = cs != null ? cs.descripcion : null
                })
            .ToListAsync(ct);

        // Nombre del servicio (adm_servicio no está en el modelo EF).
        var servicios = await ListarServiciosAsync(companyId, ct);
        var serviciosPorId = servicios.ToDictionary(s => s.ServicioId);
        foreach (var cuenta in cuentas)
        {
            if (cuenta.ServicioId.HasValue && serviciosPorId.TryGetValue(cuenta.ServicioId.Value, out var servicio))
            {
                cuenta.ServicioNombre = servicio.Display;
            }
        }

        var asientosDb = await _context.con_integracion_asientos
            .AsNoTracking()
            .Where(a => a.company_id == companyId)
            .ToListAsync(ct);

        var asientos = IntegracionContableModulos.Todos
            .Select(module =>
            {
                var fila = asientosDb.FirstOrDefault(a => a.module == module);
                return new IntegracionAsientoDto
                {
                    Module = module,
                    JournalId = fila?.journal_id,
                    TypeId = fila?.type_id,
                    Activo = config is not null && ObtenerActivo(config, module)
                };
            })
            .ToList();

        return new IntegracionContableDto
        {
            Existe = config is not null,
            Config = config is null
                ? new IntegracionConfigDto()
                : new IntegracionConfigDto
                {
                    ModoVentas = config.modo_ventas,
                    ModoCxc = config.modo_cxc,
                    EncolarSinPeriodo = config.encolar_sin_periodo
                },
            Cuentas = cuentas,
            Asientos = asientos
        };
    }

    public async Task<IntegracionGuardarResultDto> GuardarAsync(long companyId, IntegracionContableDto dto,
        string usuario, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        ArgumentNullException.ThrowIfNull(dto);
        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();

        var validacion = await ValidarInternoAsync(companyId, dto, ct);
        if (!validacion.EsValida)
        {
            throw new InvalidOperationException(
                $"La configuración tiene errores: {string.Join(" | ", validacion.Errores)}");
        }

        var ahora = DateTime.UtcNow;
        await using var transaction = await _context.Database.BeginTransactionAsync(ct);

        // Cabecera
        var config = await _context.con_integracion_configs
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
        if (config is null)
        {
            config = new con_integracion_config
            {
                company_id = companyId,
                created_at = ahora,
                created_by = usuario
            };
            _context.con_integracion_configs.Add(config);
        }
        else
        {
            config.updated_at = ahora;
            config.updated_by = usuario;
        }

        config.modo_ventas = dto.Config.ModoVentas;
        config.modo_cxc = dto.Config.ModoCxc;
        config.encolar_sin_periodo = dto.Config.EncolarSinPeriodo;
        foreach (var asiento in dto.Asientos)
        {
            AsignarActivo(config, asiento.Module, asiento.Activo);
        }

        // Matriz: diff por id (update / insert / delete)
        var cuentasExistentes = await _context.con_integracion_cuentas
            .Where(c => c.company_id == companyId)
            .ToListAsync(ct);
        var porId = cuentasExistentes.ToDictionary(c => c.integracion_cuenta_id);
        var idsEnDto = new HashSet<long>();

        foreach (var cuentaDto in dto.Cuentas)
        {
            if (cuentaDto.IntegracionCuentaId > 0 && porId.TryGetValue(cuentaDto.IntegracionCuentaId, out var existente))
            {
                idsEnDto.Add(existente.integracion_cuenta_id);
                existente.uso = cuentaDto.Uso;
                existente.servicio_id = cuentaDto.ServicioId;
                existente.categoria_servicio_id = cuentaDto.CategoriaServicioId;
                existente.con_medicion = cuentaDto.ConMedicion;
                existente.account_id = cuentaDto.AccountId!.Value;
                existente.is_active = cuentaDto.IsActive;
                existente.updated_at = ahora;
                existente.updated_by = usuario;
            }
            else
            {
                _context.con_integracion_cuentas.Add(new con_integracion_cuenta
                {
                    company_id = companyId,
                    uso = cuentaDto.Uso,
                    servicio_id = cuentaDto.ServicioId,
                    categoria_servicio_id = cuentaDto.CategoriaServicioId,
                    con_medicion = cuentaDto.ConMedicion,
                    account_id = cuentaDto.AccountId!.Value,
                    is_active = cuentaDto.IsActive,
                    created_at = ahora,
                    created_by = usuario
                });
            }
        }

        var eliminadas = cuentasExistentes.Where(c => !idsEnDto.Contains(c.integracion_cuenta_id)).ToList();
        _context.con_integracion_cuentas.RemoveRange(eliminadas);

        // Asientos por módulo (upsert)
        var asientosExistentes = await _context.con_integracion_asientos
            .Where(a => a.company_id == companyId)
            .ToListAsync(ct);
        foreach (var asientoDto in dto.Asientos)
        {
            var existente = asientosExistentes.FirstOrDefault(a => a.module == asientoDto.Module);
            if (existente is null)
            {
                if (asientoDto.JournalId.HasValue || asientoDto.TypeId.HasValue)
                {
                    _context.con_integracion_asientos.Add(new con_integracion_asiento
                    {
                        company_id = companyId,
                        module = asientoDto.Module,
                        journal_id = asientoDto.JournalId,
                        type_id = asientoDto.TypeId,
                        created_at = ahora,
                        created_by = usuario
                    });
                }
            }
            else
            {
                existente.journal_id = asientoDto.JournalId;
                existente.type_id = asientoDto.TypeId;
                existente.updated_at = ahora;
                existente.updated_by = usuario;
            }
        }

        await _context.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var persistida = await ObtenerAsync(companyId, ct);
        return new IntegracionGuardarResultDto
        {
            Configuracion = persistida,
            Validacion = validacion
        };
    }

    public async Task<IntegracionPerfilResultDto> AplicarPerfilAsync(long companyId, string perfil, string usuario,
        CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        if (string.IsNullOrWhiteSpace(perfil))
        {
            throw new ArgumentException("El perfil es obligatorio.", nameof(perfil));
        }

        usuario = string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();
        var conn = _context.Database.GetDbConnection();
        var resultado = await conn.QueryFirstAsync<(int insertadas, int existentes, int sin_cuenta)>(
            new CommandDefinition(
                "SELECT insertadas, existentes, sin_cuenta FROM public.sp_con_aplicar_perfil_integracion(@companyId, @perfil, @usuario)",
                new { companyId, perfil, usuario },
                cancellationToken: ct));

        return new IntegracionPerfilResultDto
        {
            Insertadas = resultado.insertadas,
            Existentes = resultado.existentes,
            SinCuenta = resultado.sin_cuenta
        };
    }

    public async Task<IntegracionValidacionDto> ValidarAsync(long companyId, CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        var dto = await ObtenerAsync(companyId, ct);
        return await ValidarInternoAsync(companyId, dto, ct);
    }

    public async Task<IReadOnlyList<ServicioIntegracionLookupDto>> ListarServiciosAsync(long companyId,
        CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        var conn = _context.Database.GetDbConnection();
        var filas = await conn.QueryAsync<(long servicio_id, string codigo, string? nombre)>(
            new CommandDefinition(@"
                SELECT servicio_id, codigo, nombre
                FROM public.adm_servicio
                WHERE company_id = @companyId AND status_id = 1
                ORDER BY codigo",
                new { companyId },
                cancellationToken: ct));

        return filas
            .Select(f => new ServicioIntegracionLookupDto
            {
                ServicioId = f.servicio_id,
                Codigo = f.codigo,
                Nombre = f.nombre ?? string.Empty
            })
            .ToList();
    }

    public async Task<IReadOnlyList<CuentaContableLookupDto>> ListarCuentasPosteablesAsync(long companyId,
        CancellationToken ct = default)
    {
        ValidarCompanyId(companyId);
        return await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.allows_posting)
            .OrderBy(c => c.code)
            .Select(c => new CuentaContableLookupDto
            {
                AccountId = c.account_id,
                Code = c.code,
                Description = c.name
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CategoriaServicioLookupDto>> ListarCategoriasAsync(CancellationToken ct = default)
    {
        return await _context.categoria_servicios
            .AsNoTracking()
            .Where(c => c.estado)
            .OrderBy(c => c.categoria_servicio_id)
            .Select(c => new CategoriaServicioLookupDto
            {
                CategoriaServicioId = c.categoria_servicio_id,
                Descripcion = c.descripcion
            })
            .ToListAsync(ct);
    }

    // ------------------------------------------------------------------
    // Validación (F2 punto 4): cuentas posteables y cobertura según modo
    // ------------------------------------------------------------------

    private async Task<IntegracionValidacionDto> ValidarInternoAsync(long companyId, IntegracionContableDto dto,
        CancellationToken ct)
    {
        var resultado = new IntegracionValidacionDto();
        var cuentas = dto.Cuentas;
        var asientos = dto.Asientos;
        var activos = asientos.Where(a => a.Activo).Select(a => a.Module).ToHashSet();

        // --- Errores duros de forma -------------------------------------
        foreach (var cuenta in cuentas)
        {
            var etiqueta = DescribirFila(cuenta);
            if (!IntegracionContableUsos.Todos.Contains(cuenta.Uso))
            {
                resultado.Errores.Add($"{etiqueta}: uso desconocido.");
            }

            if (cuenta.AccountId is null or <= 0)
            {
                resultado.Errores.Add($"{etiqueta}: falta seleccionar la cuenta contable.");
            }

            if (cuenta.ServicioId is null && (cuenta.CategoriaServicioId is not null || cuenta.ConMedicion is not null))
            {
                resultado.Errores.Add($"{etiqueta}: categoría y medición solo aplican bajo un servicio.");
            }
        }

        var duplicadas = cuentas
            .GroupBy(c => (c.Uso, c.ServicioId, c.CategoriaServicioId, c.ConMedicion))
            .Where(g => g.Count() > 1);
        foreach (var grupo in duplicadas)
        {
            resultado.Errores.Add($"{DescribirFila(grupo.First())}: fila duplicada (misma combinación de uso y dimensiones).");
        }

        // --- Cuentas posteables (allows_posting) -------------------------
        var accountIds = cuentas
            .Where(c => c.AccountId is > 0)
            .Select(c => c.AccountId!.Value)
            .Distinct()
            .ToList();
        if (accountIds.Count > 0)
        {
            var plan = await _context.con_plan_cuentas
                .AsNoTracking()
                .Where(p => p.company_id == companyId && accountIds.Contains(p.account_id))
                .Select(p => new { p.account_id, p.code, p.name, p.allows_posting })
                .ToDictionaryAsync(p => p.account_id, ct);

            foreach (var cuenta in cuentas.Where(c => c.AccountId is > 0))
            {
                if (!plan.TryGetValue(cuenta.AccountId!.Value, out var planCuenta))
                {
                    resultado.Errores.Add($"{DescribirFila(cuenta)}: la cuenta no existe en el plan de la empresa.");
                }
                else if (!planCuenta.allows_posting)
                {
                    resultado.Errores.Add(
                        $"{DescribirFila(cuenta)}: la cuenta {planCuenta.code} - {planCuenta.name} no permite posteo (allows_posting).");
                }
            }
        }

        // --- Asientos por módulo -----------------------------------------
        foreach (var asiento in asientos)
        {
            if (!IntegracionContableModulos.Todos.Contains(asiento.Module))
            {
                resultado.Errores.Add($"Asientos: módulo desconocido '{asiento.Module}'.");
                continue;
            }

            if (asiento.Activo && (asiento.JournalId is null || asiento.TypeId is null))
            {
                resultado.Errores.Add(
                    $"Asientos: el módulo {asiento.Module} está activo pero le falta diario y/o tipo de partida.");
            }
        }

        // --- Cobertura de la matriz según el modo ------------------------
        var activas = cuentas.Where(c => c.IsActive && c.AccountId is > 0).ToList();
        var servicios = await ListarServiciosAsync(companyId, ct);
        var categorias = await ListarCategoriasAsync(ct);
        var ventasRelacionadoActivo = activos.Overlaps(
            [IntegracionContableModulos.Facturacion, IntegracionContableModulos.Notas, IntegracionContableModulos.Miscelaneos, IntegracionContableModulos.Caja]);

        ValidarCobertura(resultado, activas, servicios, categorias,
            IntegracionContableUsos.Ingreso, dto.Config.ModoVentas, esError: ventasRelacionadoActivo);
        ValidarCobertura(resultado, activas, servicios, categorias,
            IntegracionContableUsos.Cxc, dto.Config.ModoCxc, esError: ventasRelacionadoActivo);

        // --- Usos generales puntuales ------------------------------------
        var tieneCajaGeneral = activas.Any(c => c.Uso == IntegracionContableUsos.Caja && c.ServicioId is null);
        if (!tieneCajaGeneral)
        {
            AgregarHallazgo(resultado,
                "Falta la cuenta general de CAJA.",
                esError: activos.Contains(IntegracionContableModulos.Caja) || activos.Contains(IntegracionContableModulos.Miscelaneos));
        }

        var tieneBancoDefault = activas.Any(c => c.Uso == IntegracionContableUsos.BancoDefault && c.ServicioId is null);
        if (!tieneBancoDefault)
        {
            AgregarHallazgo(resultado,
                "Falta la cuenta general BANCO_DEFAULT (contrapartida bancaria por defecto).",
                esError: activos.Contains(IntegracionContableModulos.Bancos));
        }

        if (!activas.Any(c => c.Uso == IntegracionContableUsos.DevolucionNc))
        {
            resultado.Advertencias.Add(
                "DEVOLUCION_NC sin configurar: la nota de crédito espejará las cuentas de la factura origen (comportamiento por defecto). Configúrela solo si desea una cuenta de devolución específica.");
        }

        return resultado;
    }

    /// <summary>
    /// Verifica que cada combinación exigida por el modo resuelva cuenta con la
    /// misma semántica de comodines de fn_con_resolver_cuenta. Los huecos son
    /// error si el posteo relacionado está activo; advertencia si no. Caer a la
    /// fila general en modos por servicio/categoría se reporta como advertencia.
    /// </summary>
    private static void ValidarCobertura(IntegracionValidacionDto resultado, List<IntegracionCuentaDto> activas,
        IReadOnlyList<ServicioIntegracionLookupDto> servicios, IReadOnlyList<CategoriaServicioLookupDto> categorias,
        string uso, string modo, bool esError)
    {
        var filasUso = activas.Where(c => c.Uso == uso).ToList();
        var general = filasUso.Any(c => c.ServicioId is null);

        if (modo == IntegracionContableModos.General)
        {
            if (!general)
            {
                AgregarHallazgo(resultado, $"Modo GENERAL: falta la fila general del uso {uso}.", esError);
            }
            return;
        }

        var combos = modo == IntegracionContableModos.PorServicio
            ? servicios.Select(s => (Servicio: s, Categoria: (CategoriaServicioLookupDto?)null, Medicion: (bool?)null))
            : servicios.SelectMany(s => categorias.SelectMany(cat => new (ServicioIntegracionLookupDto, CategoriaServicioLookupDto?, bool?)[]
                {
                    (s, cat, true),
                    (s, cat, false)
                }));

        var huecosDuros = new List<string>();
        var huecosBlandos = new List<string>();
        foreach (var (servicio, categoria, medicion) in combos)
        {
            var matches = filasUso.Where(f =>
                    (f.ServicioId is null || f.ServicioId == servicio.ServicioId)
                    && (f.CategoriaServicioId is null || f.CategoriaServicioId == categoria?.CategoriaServicioId)
                    && (f.ConMedicion is null || f.ConMedicion == medicion))
                .ToList();

            var descripcion = categoria is null
                ? servicio.Codigo
                : $"{servicio.Codigo} × {categoria.Descripcion} × {(medicion == true ? "con medición" : "sin medición")}";

            if (matches.Count == 0)
            {
                huecosDuros.Add(descripcion);
            }
            else if (matches.All(f => f.ServicioId is null))
            {
                huecosBlandos.Add(descripcion);
            }
        }

        if (huecosDuros.Count > 0)
        {
            AgregarHallazgo(resultado,
                $"Uso {uso} (modo {modo}): sin cuenta para {huecosDuros.Count} combinación(es): {string.Join(", ", huecosDuros.Take(10))}{(huecosDuros.Count > 10 ? ", …" : string.Empty)}.",
                esError);
        }

        if (huecosBlandos.Count > 0)
        {
            resultado.Advertencias.Add(
                $"Uso {uso}: {huecosBlandos.Count} combinación(es) usarán la fila general (fallback): {string.Join(", ", huecosBlandos.Take(10))}{(huecosBlandos.Count > 10 ? ", …" : string.Empty)}.");
        }
    }

    private static void AgregarHallazgo(IntegracionValidacionDto resultado, string mensaje, bool esError)
    {
        if (esError)
        {
            resultado.Errores.Add(mensaje);
        }
        else
        {
            resultado.Advertencias.Add(mensaje);
        }
    }

    private static string DescribirFila(IntegracionCuentaDto cuenta)
    {
        var dims = new List<string>();
        if (cuenta.ServicioId.HasValue)
        {
            dims.Add(cuenta.ServicioNombre ?? $"servicio {cuenta.ServicioId}");
        }
        if (cuenta.CategoriaServicioId.HasValue)
        {
            dims.Add(cuenta.CategoriaDescripcion ?? $"categoría {cuenta.CategoriaServicioId}");
        }
        if (cuenta.ConMedicion.HasValue)
        {
            dims.Add(cuenta.ConMedicion.Value ? "con medición" : "sin medición");
        }

        return dims.Count == 0
            ? $"Fila {cuenta.Uso} (general)"
            : $"Fila {cuenta.Uso} ({string.Join(" × ", dims)})";
    }

    private static bool ObtenerActivo(con_integracion_config config, string module) => module switch
    {
        IntegracionContableModulos.Facturacion => config.activo_facturacion,
        IntegracionContableModulos.Caja => config.activo_caja,
        IntegracionContableModulos.Bancos => config.activo_bancos,
        IntegracionContableModulos.Notas => config.activo_notas,
        IntegracionContableModulos.Miscelaneos => config.activo_miscelaneos,
        _ => false
    };

    private static void AsignarActivo(con_integracion_config config, string module, bool valor)
    {
        switch (module)
        {
            case IntegracionContableModulos.Facturacion:
                config.activo_facturacion = valor;
                break;
            case IntegracionContableModulos.Caja:
                config.activo_caja = valor;
                break;
            case IntegracionContableModulos.Bancos:
                config.activo_bancos = valor;
                break;
            case IntegracionContableModulos.Notas:
                config.activo_notas = valor;
                break;
            case IntegracionContableModulos.Miscelaneos:
                config.activo_miscelaneos = valor;
                break;
        }
    }

    private static void ValidarCompanyId(long companyId)
    {
        if (companyId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(companyId), "El ID de empresa debe ser mayor a cero.");
        }
    }
}
