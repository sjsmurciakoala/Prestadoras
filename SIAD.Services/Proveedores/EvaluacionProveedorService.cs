using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Infrastructure;

namespace SIAD.Services.Proveedores;

/// <summary>
/// Implementación del scorecard de proveedores.
/// <para>
/// El acceso a datos va por Dapper: las métricas automáticas salen de
/// <c>fn_prv_evaluacion_metricas</c> (una pasada por período) y el resto son consultas directas
/// contra <c>prv_evaluacion_*</c>. Aquí viven las tres reglas del modelo:
/// </para>
/// <list type="number">
/// <item>el detalle guarda <b>snapshot</b> del código, nombre y peso del criterio, así que
/// repesar el catálogo no reescribe la historia ya calculada;</item>
/// <item>un criterio <b>sin denominador no puntúa cero</b>: se excluye y su peso se reparte
/// proporcionalmente entre los que sí tienen datos (ver <see cref="AplicarPesos"/>);</item>
/// <item>recalcular un período <b>respeta lo capturado a mano</b> en los criterios manuales.</item>
/// </list>
/// <para>
/// <b>Tenancy:</b> Dapper no pasa por el filtro global de <see cref="SiadDbContext"/>, así que la
/// empresa se resuelve con <see cref="ICurrentCompanyService"/> y viaja explícita en cada consulta.
/// </para>
/// </summary>
public sealed class EvaluacionProveedorService : IEvaluacionProveedorService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    /// <summary>Tolerancia de precio por defecto (%), si el criterio no trae parámetro.</summary>
    private const decimal ToleranciaPrecioPorDefecto = 2.0m;

    public EvaluacionProveedorService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;

        // Dapper no sabe pasar DateOnly sin este handler. Idempotente; va aquí para que también
        // aplique cuando el servicio se instancia a mano (tests).
        DapperTypeHandlers.EnsureRegistered();
    }

    // ── Períodos ─────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EvaluacionPeriodoDto>> GetPeriodosAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        const string sql = @"
            SELECT p.id             AS Id,
                   p.codigo         AS Codigo,
                   p.nombre         AS Nombre,
                   p.fecha_desde    AS FechaDesde,
                   p.fecha_hasta    AS FechaHasta,
                   p.estado         AS Estado,
                   p.fecha_calculo  AS FechaCalculo,
                   p.usuario_calculo AS UsuarioCalculo,
                   p.fecha_cierre   AS FechaCierre,
                   p.usuario_cierre AS UsuarioCierre,
                   (SELECT count(*) FROM public.prv_evaluacion_hdr h
                     WHERE h.company_id = p.company_id AND h.periodo_id = p.id) AS Evaluaciones
              FROM public.prv_evaluacion_periodo p
             WHERE p.company_id = @CompanyId
             ORDER BY p.fecha_desde DESC";

        var filas = await connection.QueryAsync<EvaluacionPeriodoDto>(
            new CommandDefinition(sql, new { CompanyId = companyId }, TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<EvaluacionPeriodoDto?> GetPeriodoAsync(int periodoId, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        return await LeerPeriodoAsync(connection, TransaccionActual(), companyId, periodoId, ct);
    }

    public async Task<EvaluacionPeriodoDto> CrearPeriodoAsync(
        EvaluacionPeriodoUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();

        var codigo = (dto.Codigo ?? string.Empty).Trim();
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        if (codigo.Length == 0) throw new InvalidOperationException("El código del período es obligatorio.");
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre del período es obligatorio.");
        if (dto.FechaHasta < dto.FechaDesde)
        {
            throw new InvalidOperationException("La fecha final del período no puede ser anterior a la inicial.");
        }

        var connection = await AbrirConexionAsync(ct);

        var existe = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            SELECT EXISTS (SELECT 1 FROM public.prv_evaluacion_periodo
                            WHERE company_id = @CompanyId AND codigo = @Codigo)",
            new { CompanyId = companyId, Codigo = codigo }, TransaccionActual(), cancellationToken: ct));
        if (existe)
        {
            throw new InvalidOperationException($"Ya existe un período con el código «{codigo}».");
        }

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.prv_evaluacion_periodo
                   (company_id, codigo, nombre, fecha_desde, fecha_hasta, estado, usuariocreacion, fechacreacion)
            VALUES (@CompanyId, @Codigo, @Nombre, @Desde, @Hasta, @Estado, @Usuario, @Ahora)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                Codigo = codigo,
                Nombre = nombre,
                Desde = dto.FechaDesde,
                Hasta = dto.FechaHasta,
                Estado = EstadoEvaluacionPeriodo.Abierto,
                Usuario = Usuario(usuario),
                Ahora = Ahora()
            }, TransaccionActual(), cancellationToken: ct));

        return (await LeerPeriodoAsync(connection, TransaccionActual(), companyId, id, ct))!;
    }

    public async Task<bool> CerrarPeriodoAsync(int periodoId, string usuario, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        var periodo = await LeerPeriodoAsync(connection, TransaccionActual(), companyId, periodoId, ct);
        if (periodo is null) return false;
        if (periodo.Cerrado) return true;
        if (periodo.Evaluaciones == 0)
        {
            throw new InvalidOperationException("No se puede cerrar un período sin evaluaciones calculadas.");
        }

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.prv_evaluacion_periodo
               SET estado = @Cerrado, fecha_cierre = @Ahora, usuario_cierre = @Usuario,
                   usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id;
            UPDATE public.prv_evaluacion_hdr
               SET estado = @EvalCerrada, usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND periodo_id = @Id",
            new
            {
                CompanyId = companyId,
                Id = periodoId,
                Cerrado = EstadoEvaluacionPeriodo.Cerrado,
                EvalCerrada = EstadoEvaluacionProveedor.Cerrada,
                Usuario = Usuario(usuario),
                Ahora = Ahora()
            }, TransaccionActual(), cancellationToken: ct));

        return true;
    }

    // ── Cálculo ──────────────────────────────────────────────────────────────

    public async Task<EvaluacionCalculoResultadoDto> CalcularAsync(
        int periodoId, string usuario, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        var lectura = TransaccionActual();
        var periodo = await LeerPeriodoAsync(connection, lectura, companyId, periodoId, ct)
            ?? throw new KeyNotFoundException("El período de evaluación no existe.");
        if (periodo.Cerrado)
        {
            throw new InvalidOperationException("El período está cerrado: ya no se puede recalcular.");
        }

        var criterios = await LeerCriteriosAsync(connection, lectura, companyId, ct);
        if (criterios.Count == 0)
        {
            throw new InvalidOperationException(
                "No hay criterios de evaluación configurados para la empresa.");
        }

        var clases = await LeerClasesAsync(connection, lectura, companyId, ct);
        var metricas = await LeerMetricasAsync(connection, lectura, companyId, periodo, criterios, ct);

        var usuarioNormalizado = Usuario(usuario);
        var ahora = Ahora();

        // Transacción propia sólo si no hay una ambiente (los tests envuelven todo en BEGIN…ROLLBACK).
        var ambiente = TransaccionActual();
        DbTransaction? propia = null;
        if (ambiente is null)
        {
            propia = await connection.BeginTransactionAsync(ct);
        }
        var tx = ambiente ?? propia;

        try
        {
            var evaluados = 0;
            decimal sumaPuntajes = 0m;
            var conPuntaje = 0;
            var sinDatosGlobal = new List<string>();
            var criteriosConDatos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var m in metricas)
            {
                var resultados = ArmarResultados(criterios, m);

                // Lo capturado a mano no se pierde al recalcular.
                var capturas = await LeerCapturasAsync(connection, tx, companyId, periodoId, m.CodProveedor, ct);
                foreach (var r in resultados)
                {
                    if (!r.EsManual) continue;
                    if (!capturas.TryGetValue(r.CriterioCodigo, out var captura)) continue;

                    r.Logro = captura.Logro;
                    r.UsuarioCaptura = captura.Usuario;
                    r.FechaCaptura = captura.Fecha;
                    r.Detalle = DetalleManual(captura.Usuario, captura.Fecha, captura.Logro);
                }

                AplicarPesos(resultados);
                var puntaje = CalcularPuntaje(resultados);
                var clase = ResolverClase(clases, puntaje);

                var evaluacionId = await GuardarCabeceraAsync(
                    connection, tx, companyId, periodoId, m, puntaje, clase, usuarioNormalizado, ahora, ct);
                await GuardarDetalleAsync(connection, tx, companyId, evaluacionId, resultados, ct);

                evaluados++;
                if (puntaje.HasValue)
                {
                    sumaPuntajes += puntaje.Value;
                    conPuntaje++;
                }

                foreach (var r in resultados)
                {
                    if (!r.SinDatos) criteriosConDatos.Add(r.CriterioCodigo);
                }
            }

            foreach (var c in criterios)
            {
                if (!criteriosConDatos.Contains(c.Codigo)) sinDatosGlobal.Add(c.Nombre);
            }

            await connection.ExecuteAsync(new CommandDefinition(@"
                UPDATE public.prv_evaluacion_periodo
                   SET fecha_calculo = @Ahora, usuario_calculo = @Usuario,
                       usuariomodificacion = @Usuario, fechamodificacion = @Ahora
                 WHERE company_id = @CompanyId AND id = @Id",
                new { CompanyId = companyId, Id = periodoId, Usuario = usuarioNormalizado, Ahora = ahora },
                tx, cancellationToken: ct));

            if (propia is not null)
            {
                await propia.CommitAsync(ct);
            }

            return new EvaluacionCalculoResultadoDto
            {
                PeriodoId = periodoId,
                Evaluados = evaluados,
                PromedioPuntaje = conPuntaje > 0
                    ? Math.Round(sumaPuntajes / conPuntaje, 2, MidpointRounding.AwayFromZero)
                    : null,
                FechaCalculo = ahora,
                CriteriosSinDatos = sinDatosGlobal
            };
        }
        catch
        {
            if (propia is not null)
            {
                await propia.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (propia is not null)
            {
                await propia.DisposeAsync();
            }
        }
    }

    // ── Consultas ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<EvaluacionRankingItemDto>> GetRankingAsync(
        int periodoId, EvaluacionFilterDto? filtro = null, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        filtro ??= new EvaluacionFilterDto();
        var search = string.IsNullOrWhiteSpace(filtro.Search) ? null : $"%{filtro.Search.Trim()}%";
        var clase = string.IsNullOrWhiteSpace(filtro.ClaseCodigo) ? null : filtro.ClaseCodigo.Trim();

        const string sql = @"
            SELECT h.id               AS Id,
                   h.cod_proveedor    AS CodProveedor,
                   h.proveedor_nombre AS ProveedorNombre,
                   h.puntaje          AS Puntaje,
                   h.clase_codigo     AS ClaseCodigo,
                   cl.nombre          AS ClaseNombre,
                   h.compras_periodo  AS ComprasPeriodo,
                   h.recepciones      AS Recepciones,
                   h.ordenes          AS Ordenes,
                   h.estado           AS Estado
              FROM public.prv_evaluacion_hdr h
              LEFT JOIN public.prv_evaluacion_clase cl
                     ON cl.company_id = h.company_id AND cl.id = h.clase_id
             WHERE h.company_id = @CompanyId
               AND h.periodo_id = @PeriodoId
               -- Casts explícitos: con el filtro en NULL, Postgres no infiere el tipo desde
               -- «@X IS NULL» y aborta con 42P08.
               AND (@Search::text    IS NULL OR h.cod_proveedor ILIKE @Search::text
                                             OR COALESCE(h.proveedor_nombre, '') ILIKE @Search::text)
               AND (@Clase::varchar  IS NULL OR h.clase_codigo = @Clase::varchar)
               AND (@Minimo::numeric IS NULL OR h.compras_periodo >= @Minimo::numeric)
             ORDER BY h.puntaje DESC NULLS LAST, h.compras_periodo DESC";

        var filas = (await connection.QueryAsync<EvaluacionRankingItemDto>(new CommandDefinition(sql,
            new
            {
                CompanyId = companyId,
                PeriodoId = periodoId,
                Search = search,
                Clase = clase,
                Minimo = filtro.ComprasMinimas
            }, tx, cancellationToken: ct))).AsList();

        if (filas.Count == 0) return filas;

        var detalles = await LeerDetallePeriodoAsync(connection, tx, companyId, periodoId, ct);
        foreach (var fila in filas)
        {
            if (detalles.TryGetValue(fila.Id, out var lista)) fila.Criterios = lista;
        }

        // Tendencia: puntaje del período inmediatamente anterior por fecha.
        var anteriores = await LeerPuntajesPeriodoAnteriorAsync(connection, tx, companyId, periodoId, ct);
        if (anteriores.Count > 0)
        {
            foreach (var fila in filas)
            {
                if (anteriores.TryGetValue(fila.CodProveedor, out var puntaje)) fila.PuntajeAnterior = puntaje;
            }
        }

        return filas;
    }

    public async Task<EvaluacionFichaDto?> GetFichaAsync(
        int periodoId, string codProveedor, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        return await LeerFichaAsync(connection, TransaccionActual(), companyId, periodoId, codProveedor, ct);
    }

    public async Task<EvaluacionFichaDto> CapturarAsync(
        int periodoId, string codProveedor, EvaluacionCapturaDto dto, string usuario,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var cod = (codProveedor ?? string.Empty).Trim();
        var connection = await AbrirConexionAsync(ct);
        var ambiente = TransaccionActual();

        var periodo = await LeerPeriodoAsync(connection, ambiente, companyId, periodoId, ct)
            ?? throw new KeyNotFoundException("El período de evaluación no existe.");
        if (periodo.Cerrado)
        {
            throw new InvalidOperationException("El período está cerrado: ya no admite cambios.");
        }

        var ficha = await LeerFichaAsync(connection, ambiente, companyId, periodoId, cod, ct)
            ?? throw new KeyNotFoundException("El proveedor no tiene evaluación en este período.");

        var usuarioNormalizado = Usuario(usuario);
        var ahora = Ahora();

        if (!string.IsNullOrWhiteSpace(dto.CriterioCodigo))
        {
            var codigo = dto.CriterioCodigo.Trim();
            EvaluacionCriterioResultadoDto? objetivo = null;
            foreach (var c in ficha.Criterios)
            {
                if (string.Equals(c.CriterioCodigo, codigo, StringComparison.OrdinalIgnoreCase))
                {
                    objetivo = c;
                    break;
                }
            }

            if (objetivo is null)
            {
                throw new InvalidOperationException("Ese criterio no forma parte de la evaluación.");
            }
            if (!objetivo.EsManual)
            {
                throw new InvalidOperationException(
                    $"«{objetivo.CriterioNombre}» se calcula automáticamente: no se califica a mano.");
            }
            if (dto.Logro is < 0m or > 100m)
            {
                throw new InvalidOperationException("La calificación debe estar entre 0 y 100.");
            }

            objetivo.Logro = dto.Logro;
            objetivo.UsuarioCaptura = dto.Logro.HasValue ? usuarioNormalizado : null;
            objetivo.FechaCaptura = dto.Logro.HasValue ? ahora : null;
            objetivo.Detalle = DetalleManual(objetivo.UsuarioCaptura, objetivo.FechaCaptura, dto.Logro);
        }

        // Capturar cambia el puntaje: el peso del criterio deja de redistribuirse.
        AplicarPesos(ficha.Criterios);
        var puntaje = CalcularPuntaje(ficha.Criterios);
        var clases = await LeerClasesAsync(connection, ambiente, companyId, ct);
        var clase = ResolverClase(clases, puntaje);

        // La cabecera y sus renglones se actualizan juntos: si algo falla a mitad, el puntaje
        // guardado dejaría de corresponder con el detalle que lo explica.
        DbTransaction? propia = null;
        if (ambiente is null)
        {
            propia = await connection.BeginTransactionAsync(ct);
        }
        var tx = ambiente ?? propia;

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
                UPDATE public.prv_evaluacion_hdr
                   SET puntaje = @Puntaje, clase_id = @ClaseId, clase_codigo = @ClaseCodigo,
                       observaciones = COALESCE(@Observaciones, observaciones),
                       usuariomodificacion = @Usuario, fechamodificacion = @Ahora
                 WHERE company_id = @CompanyId AND id = @Id",
                new
                {
                    CompanyId = companyId,
                    Id = ficha.Id,
                    Puntaje = puntaje,
                    ClaseId = clase?.Id,
                    ClaseCodigo = clase?.Codigo,
                    Observaciones = dto.Observaciones,
                    Usuario = usuarioNormalizado,
                    Ahora = ahora
                }, tx, cancellationToken: ct));

            foreach (var r in ficha.Criterios)
            {
                await connection.ExecuteAsync(new CommandDefinition(@"
                    UPDATE public.prv_evaluacion_dtl
                       SET logro = @Logro, peso_efectivo = @PesoEfectivo, puntos = @Puntos,
                           detalle = @Detalle, usuario_captura = @UsuarioCaptura,
                           fecha_captura = @FechaCaptura
                     WHERE company_id = @CompanyId AND evaluacion_id = @EvaluacionId
                       AND criterio_codigo = @Codigo",
                    new
                    {
                        CompanyId = companyId,
                        EvaluacionId = ficha.Id,
                        Codigo = r.CriterioCodigo,
                        Logro = r.Logro,
                        PesoEfectivo = r.PesoEfectivo,
                        Puntos = r.Puntos,
                        Detalle = r.Detalle,
                        UsuarioCaptura = r.UsuarioCaptura,
                        FechaCaptura = r.FechaCaptura
                    }, tx, cancellationToken: ct));
            }

            if (propia is not null)
            {
                await propia.CommitAsync(ct);
            }
        }
        catch
        {
            if (propia is not null)
            {
                await propia.RollbackAsync(ct);
            }
            throw;
        }
        finally
        {
            if (propia is not null)
            {
                await propia.DisposeAsync();
            }
        }

        return (await LeerFichaAsync(connection, ambiente, companyId, periodoId, cod, ct))!;
    }

    public async Task<IReadOnlyList<EvaluacionCriterioDto>> GetCriteriosAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        return await LeerCriteriosAsync(connection, TransaccionActual(), companyId, ct);
    }

    public async Task<IReadOnlyList<EvaluacionClaseDto>> GetClasesAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        return await LeerClasesAsync(connection, TransaccionActual(), companyId, ct);
    }

    // ── Catálogo: criterios (F3) ─────────────────────────────────────────────

    public async Task<IReadOnlyList<EvaluacionCriterioDto>> GetCriteriosCatalogoAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        const string sql = @"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   peso AS Peso, origen AS Origen, metrica AS Metrica, meta AS Meta,
                   parametro AS Parametro, orden AS Orden, activo AS Activo
              FROM public.prv_evaluacion_criterio
             WHERE company_id = @CompanyId
             ORDER BY orden, codigo";

        var filas = await connection.QueryAsync<EvaluacionCriterioDto>(
            new CommandDefinition(sql, new { CompanyId = companyId },
                TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<EvaluacionCriterioDto> CrearCriterioAsync(
        EvaluacionCriterioUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var (codigo, nombre, metrica) = ValidarCriterio(dto);
        await ExigirCodigoLibreAsync(connection, tx, companyId, codigo, null, ct);
        await ExigirMetricaLibreAsync(connection, tx, companyId, metrica, null, ct);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.prv_evaluacion_criterio
                   (company_id, codigo, nombre, descripcion, peso, origen, metrica, meta,
                    parametro, orden, activo, usuariocreacion, fechacreacion)
            VALUES (@CompanyId, @Codigo, @Nombre, @Descripcion, @Peso, @Origen, @Metrica, @Meta,
                    @Parametro, @Orden, @Activo, @Usuario, @Ahora)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                Codigo = codigo,
                Nombre = nombre,
                dto.Descripcion,
                dto.Peso,
                dto.Origen,
                Metrica = metrica,
                dto.Meta,
                dto.Parametro,
                dto.Orden,
                dto.Activo,
                Usuario = Usuario(usuario),
                Ahora = Ahora()
            }, tx, cancellationToken: ct));

        return (await LeerCriterioAsync(connection, tx, companyId, id, ct))!;
    }

    public async Task<EvaluacionCriterioDto> ActualizarCriterioAsync(
        int id, EvaluacionCriterioUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        _ = await LeerCriterioAsync(connection, tx, companyId, id, ct)
            ?? throw new KeyNotFoundException("El criterio no existe.");

        var (codigo, nombre, metrica) = ValidarCriterio(dto);
        await ExigirCodigoLibreAsync(connection, tx, companyId, codigo, id, ct);
        await ExigirMetricaLibreAsync(connection, tx, companyId, metrica, id, ct);

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.prv_evaluacion_criterio
               SET codigo = @Codigo, nombre = @Nombre, descripcion = @Descripcion, peso = @Peso,
                   origen = @Origen, metrica = @Metrica, meta = @Meta, parametro = @Parametro,
                   orden = @Orden, activo = @Activo,
                   usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id",
            new
            {
                CompanyId = companyId,
                Id = id,
                Codigo = codigo,
                Nombre = nombre,
                dto.Descripcion,
                dto.Peso,
                dto.Origen,
                Metrica = metrica,
                dto.Meta,
                dto.Parametro,
                dto.Orden,
                dto.Activo,
                Usuario = Usuario(usuario),
                Ahora = Ahora()
            }, tx, cancellationToken: ct));

        return (await LeerCriterioAsync(connection, tx, companyId, id, ct))!;
    }

    public async Task<bool> EliminarCriterioAsync(int id, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var criterio = await LeerCriterioAsync(connection, tx, companyId, id, ct);
        if (criterio is null) return false;

        // El detalle guarda snapshot, así que borrar no rompe la historia; pero sí la deja sin
        // catálogo detrás. Se exige desactivar para que quede claro qué pasó.
        var usado = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            SELECT EXISTS (SELECT 1 FROM public.prv_evaluacion_dtl
                            WHERE company_id = @CompanyId AND criterio_codigo = @Codigo)",
            new { CompanyId = companyId, criterio.Codigo }, tx, cancellationToken: ct));

        if (usado)
        {
            throw new InvalidOperationException(
                $"«{criterio.Nombre}» ya se usó en evaluaciones: desactívelo en vez de borrarlo.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.prv_evaluacion_criterio WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

        return true;
    }

    // ── Catálogo: clases (F3) ────────────────────────────────────────────────

    public async Task<EvaluacionClaseDto> CrearClaseAsync(
        EvaluacionClaseUpsertDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var (codigo, nombre) = ValidarClase(dto);
        await ExigirClaseSinSolaparAsync(connection, tx, companyId, dto, codigo, null, ct);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.prv_evaluacion_clase
                   (company_id, codigo, nombre, descripcion, puntaje_desde, puntaje_hasta, orden, activo)
            VALUES (@CompanyId, @Codigo, @Nombre, @Descripcion, @Desde, @Hasta, @Orden, @Activo)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                Codigo = codigo,
                Nombre = nombre,
                dto.Descripcion,
                Desde = dto.PuntajeDesde,
                Hasta = dto.PuntajeHasta,
                dto.Orden,
                dto.Activo
            }, tx, cancellationToken: ct));

        return (await LeerClaseAsync(connection, tx, companyId, id, ct))!;
    }

    public async Task<EvaluacionClaseDto> ActualizarClaseAsync(
        int id, EvaluacionClaseUpsertDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        _ = await LeerClaseAsync(connection, tx, companyId, id, ct)
            ?? throw new KeyNotFoundException("La clase no existe.");

        var (codigo, nombre) = ValidarClase(dto);
        await ExigirClaseSinSolaparAsync(connection, tx, companyId, dto, codigo, id, ct);

        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.prv_evaluacion_clase
               SET codigo = @Codigo, nombre = @Nombre, descripcion = @Descripcion,
                   puntaje_desde = @Desde, puntaje_hasta = @Hasta, orden = @Orden, activo = @Activo
             WHERE company_id = @CompanyId AND id = @Id",
            new
            {
                CompanyId = companyId,
                Id = id,
                Codigo = codigo,
                Nombre = nombre,
                dto.Descripcion,
                Desde = dto.PuntajeDesde,
                Hasta = dto.PuntajeHasta,
                dto.Orden,
                dto.Activo
            }, tx, cancellationToken: ct));

        return (await LeerClaseAsync(connection, tx, companyId, id, ct))!;
    }

    public async Task<bool> EliminarClaseAsync(int id, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        // Las evaluaciones referencian la clase por FK (ON DELETE RESTRICT): si está en uso,
        // Postgres rechazaría el DELETE con un 23503 ilegible. Mejor decirlo en castellano.
        var usada = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            SELECT EXISTS (SELECT 1 FROM public.prv_evaluacion_hdr
                            WHERE company_id = @CompanyId AND clase_id = @Id)",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

        if (usada)
        {
            throw new InvalidOperationException(
                "Esa clase ya está asignada a evaluaciones: desactívela en vez de borrarla.");
        }

        var filas = await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.prv_evaluacion_clase WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

        return filas > 0;
    }

    // ── Impresión (F5) ───────────────────────────────────────────────────────

    public async Task<EvaluacionFichaImpresionDto?> GetDatosFichaImpresionAsync(
        int periodoId, string codProveedor, string? impresoPor = null, CancellationToken ct = default)
    {
        var ficha = await GetFichaAsync(periodoId, codProveedor, ct);
        if (ficha is null) return null;

        var datos = new EvaluacionFichaImpresionDto
        {
            Ficha = ficha,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim()
        };

        if (ficha.CriteriosSinDatos > 0)
        {
            datos.NotaCriteriosSinDatos =
                $"{ficha.CriteriosSinDatos} criterio(s) no tuvieron datos en el período y no puntúan: "
                + "su peso se repartió entre los demás, por eso el peso aplicado difiere del configurado.";
        }

        await LlenarEmpresaAsync(datos, ct);
        return datos;
    }

    public async Task<EvaluacionComparativoImpresionDto?> GetDatosComparativoImpresionAsync(
        int periodoId, EvaluacionFilterDto? filtro = null, string? impresoPor = null,
        CancellationToken ct = default)
    {
        var periodo = await GetPeriodoAsync(periodoId, ct);
        if (periodo is null) return null;

        var items = await GetRankingAsync(periodoId, filtro, ct);
        var criterios = await GetCriteriosAsync(ct);

        decimal suma = 0m;
        var conPuntaje = 0;
        foreach (var item in items)
        {
            if (!item.Puntaje.HasValue) continue;
            suma += item.Puntaje.Value;
            conPuntaje++;
        }

        var datos = new EvaluacionComparativoImpresionDto
        {
            PeriodoCodigo = periodo.Codigo,
            PeriodoNombre = periodo.Nombre,
            FechaDesde = periodo.FechaDesde,
            FechaHasta = periodo.FechaHasta,
            PeriodoCerrado = periodo.Cerrado,
            Criterios = new List<EvaluacionCriterioDto>(criterios),
            Items = new List<EvaluacionRankingItemDto>(items),
            PromedioPuntaje = conPuntaje > 0
                ? Math.Round(suma / conPuntaje, 2, MidpointRounding.AwayFromZero)
                : null,
            FiltroTexto = DescribirFiltro(filtro),
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim()
        };

        await LlenarEmpresaAsync(datos, ct);
        return datos;
    }

    /// <summary>Membrete: los datos de la empresa del tenant actual.</summary>
    private async Task LlenarEmpresaAsync(ComprobanteAlmacenImpresionBase datos, CancellationToken ct)
    {
        var companyId = EnsureCompanyId();
        var empresa = await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);

        datos.EmpresaNombre = empresa?.commercial_name ?? string.Empty;
        datos.EmpresaRazonSocial = empresa?.legal_name;
        datos.EmpresaRtn = empresa?.tax_id;
        datos.EmpresaDireccion = empresa?.address;
        datos.EmpresaTelefono = empresa?.phone;
        datos.EmpresaEmail = empresa?.email;
        datos.EmpresaLogo = empresa?.logo;
    }

    /// <summary>Frase legible del filtro, para que el papel diga qué se está viendo.</summary>
    private static string? DescribirFiltro(EvaluacionFilterDto? filtro)
    {
        if (filtro is null) return null;

        var partes = new List<string>();
        if (!string.IsNullOrWhiteSpace(filtro.Search)) partes.Add($"búsqueda «{filtro.Search.Trim()}»");
        if (!string.IsNullOrWhiteSpace(filtro.ClaseCodigo)) partes.Add($"clase {filtro.ClaseCodigo.Trim()}");
        if (filtro.ComprasMinimas.HasValue) partes.Add($"compras desde {filtro.ComprasMinimas.Value:N2}");

        return partes.Count == 0 ? null : "Filtro aplicado: " + string.Join(" · ", partes) + ".";
    }

    // ── Reglas del modelo ────────────────────────────────────────────────────

    /// <summary>
    /// Reparte el peso de los criterios SIN datos entre los que sí los tienen y calcula los puntos.
    /// <para>
    /// Sin esta regla, un criterio sin denominador (por ejemplo, entrega cuando ninguna orden del
    /// período traía fecha pactada) puntuaría cero y reprobaría a todos los proveedores por un
    /// hueco de datos, no por su desempeño.
    /// </para>
    /// </summary>
    private static void AplicarPesos(List<EvaluacionCriterioResultadoDto> criterios)
    {
        decimal pesoConDatos = 0m;
        foreach (var c in criterios)
        {
            if (c.Logro.HasValue) pesoConDatos += c.Peso;
        }

        foreach (var c in criterios)
        {
            if (!c.Logro.HasValue || pesoConDatos <= 0m)
            {
                c.PesoEfectivo = null;
                c.Puntos = null;
                continue;
            }

            c.PesoEfectivo = Math.Round(c.Peso * 100m / pesoConDatos, 2, MidpointRounding.AwayFromZero);
            c.Puntos = Math.Round(c.PesoEfectivo.Value * c.Logro.Value / 100m, 2, MidpointRounding.AwayFromZero);
        }
    }

    /// <summary>Σ de puntos. NULL cuando ningún criterio tuvo datos: no es lo mismo que cero.</summary>
    private static decimal? CalcularPuntaje(List<EvaluacionCriterioResultadoDto> criterios)
    {
        decimal total = 0m;
        var alguno = false;
        foreach (var c in criterios)
        {
            if (!c.Puntos.HasValue) continue;
            total += c.Puntos.Value;
            alguno = true;
        }

        return alguno ? Math.Round(total, 2, MidpointRounding.AwayFromZero) : null;
    }

    /// <summary>
    /// Clase que corresponde al puntaje: la de mayor <c>puntaje_desde</c> que no lo supere. Se
    /// resuelve así —y no con un BETWEEN— para que un hueco por redondeo entre rangos (89.995)
    /// no deje al proveedor sin clase.
    /// </summary>
    private static EvaluacionClaseDto? ResolverClase(IReadOnlyList<EvaluacionClaseDto> clases, decimal? puntaje)
    {
        if (!puntaje.HasValue) return null;

        EvaluacionClaseDto? elegida = null;
        foreach (var c in clases)
        {
            if (c.PuntajeDesde > puntaje.Value) continue;
            if (elegida is null || c.PuntajeDesde > elegida.PuntajeDesde) elegida = c;
        }

        return elegida;
    }

    /// <summary>Cruza el catálogo de criterios con las métricas del proveedor.</summary>
    private static List<EvaluacionCriterioResultadoDto> ArmarResultados(
        IReadOnlyList<EvaluacionCriterioDto> criterios, MetricaProveedor m)
    {
        var lista = new List<EvaluacionCriterioResultadoDto>(criterios.Count);

        foreach (var c in criterios)
        {
            var r = new EvaluacionCriterioResultadoDto
            {
                CriterioCodigo = c.Codigo,
                CriterioNombre = c.Nombre,
                Peso = c.Peso,
                Origen = c.Origen,
                Metrica = c.Metrica
            };

            if (c.EsManual || string.IsNullOrWhiteSpace(c.Metrica))
            {
                r.Detalle = "Pendiente de calificar.";
                lista.Add(r);
                continue;
            }

            var (num, den, textoConDatos, textoSinDatos) = m.Resolver(c.Metrica);
            r.Numerador = num;
            r.Denominador = den;

            if (den > 0m)
            {
                r.Logro = Math.Round(num / den * 100m, 2, MidpointRounding.AwayFromZero);
                if (r.Logro > 100m) r.Logro = 100m;   // completitud puede pasarse si se recibe de más
                r.Detalle = textoConDatos;
            }
            else
            {
                r.Detalle = textoSinDatos;
            }

            lista.Add(r);
        }

        return lista;
    }

    // ── Validaciones del catálogo (F3) ───────────────────────────────────────

    /// <summary>
    /// Cotas de un criterio. Devuelve código, nombre y métrica ya normalizados.
    /// <para>
    /// La métrica es obligatoria en los automáticos —sin ella el criterio no tendría de dónde
    /// salir y quedaría eternamente "sin datos"— y se fuerza a NULL en los manuales.
    /// </para>
    /// </summary>
    private static (string Codigo, string Nombre, string? Metrica) ValidarCriterio(EvaluacionCriterioUpsertDto dto)
    {
        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = (dto.Nombre ?? string.Empty).Trim();

        if (codigo.Length == 0) throw new InvalidOperationException("El código del criterio es obligatorio.");
        if (codigo.Length > 20) throw new InvalidOperationException("El código no puede superar 20 caracteres.");
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre del criterio es obligatorio.");
        if (nombre.Length > 100) throw new InvalidOperationException("El nombre no puede superar 100 caracteres.");
        if (dto.Peso is < 0m or > 100m) throw new InvalidOperationException("El peso debe estar entre 0 y 100.");
        if (dto.Meta is < 0m or > 100m) throw new InvalidOperationException("La meta debe estar entre 0 y 100.");
        if (dto.Parametro is < 0m) throw new InvalidOperationException("El parámetro no puede ser negativo.");

        if (dto.Origen is not (OrigenCriterioEvaluacion.Automatico or OrigenCriterioEvaluacion.Manual))
        {
            throw new InvalidOperationException("El origen del criterio no es válido.");
        }

        if (dto.Origen == OrigenCriterioEvaluacion.Manual)
        {
            return (codigo, nombre, null);
        }

        var metrica = (dto.Metrica ?? string.Empty).Trim().ToUpperInvariant();
        if (metrica.Length == 0)
        {
            throw new InvalidOperationException(
                "Un criterio automático necesita una métrica: sin ella nunca tendría datos.");
        }

        if (metrica is not (MetricaEvaluacion.Entrega
                         or MetricaEvaluacion.Completo
                         or MetricaEvaluacion.Precio
                         or MetricaEvaluacion.Calidad
                         or MetricaEvaluacion.Documento))
        {
            throw new InvalidOperationException(
                $"La métrica «{metrica}» no existe. Válidas: ENTREGA, COMPLETO, PRECIO, CALIDAD y DOCUMENTO.");
        }

        return (codigo, nombre, metrica);
    }

    private static (string Codigo, string Nombre) ValidarClase(EvaluacionClaseUpsertDto dto)
    {
        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = (dto.Nombre ?? string.Empty).Trim();

        if (codigo.Length == 0) throw new InvalidOperationException("El código de la clase es obligatorio.");
        if (codigo.Length > 10) throw new InvalidOperationException("El código no puede superar 10 caracteres.");
        if (nombre.Length == 0) throw new InvalidOperationException("El nombre de la clase es obligatorio.");
        if (dto.PuntajeDesde is < 0m or > 100m || dto.PuntajeHasta is < 0m or > 100m)
        {
            throw new InvalidOperationException("Los puntajes de la clase deben estar entre 0 y 100.");
        }
        if (dto.PuntajeHasta < dto.PuntajeDesde)
        {
            throw new InvalidOperationException("El puntaje final no puede ser menor que el inicial.");
        }

        return (codigo, nombre);
    }

    private static async Task ExigirCodigoLibreAsync(
        DbConnection connection, DbTransaction? tx, long companyId, string codigo, int? excepto,
        CancellationToken ct)
    {
        var existe = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            SELECT EXISTS (SELECT 1 FROM public.prv_evaluacion_criterio
                            WHERE company_id = @CompanyId AND codigo = @Codigo
                              AND (@Excepto::integer IS NULL OR id <> @Excepto::integer))",
            new { CompanyId = companyId, Codigo = codigo, Excepto = excepto }, tx, cancellationToken: ct));

        if (existe)
        {
            throw new InvalidOperationException($"Ya existe un criterio con el código «{codigo}».");
        }
    }

    /// <summary>
    /// Dos criterios con la misma métrica medirían lo mismo dos veces y se llevarían el doble de
    /// peso. Sólo se controla entre los ACTIVOS: uno inactivo no participa del cálculo.
    /// </summary>
    private static async Task ExigirMetricaLibreAsync(
        DbConnection connection, DbTransaction? tx, long companyId, string? metrica, int? excepto,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(metrica)) return;

        var ocupada = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT nombre FROM public.prv_evaluacion_criterio
             WHERE company_id = @CompanyId AND activo = TRUE AND metrica = @Metrica
               AND (@Excepto::integer IS NULL OR id <> @Excepto::integer)
             LIMIT 1",
            new { CompanyId = companyId, Metrica = metrica, Excepto = excepto }, tx, cancellationToken: ct));

        if (!string.IsNullOrWhiteSpace(ocupada))
        {
            throw new InvalidOperationException(
                $"La métrica {metrica} ya la usa «{ocupada}»: dos criterios activos no pueden medir lo mismo.");
        }
    }

    /// <summary>
    /// Las clases activas no pueden solaparse: con dos rangos que se pisan, el mismo puntaje
    /// caería en dos clases y la resolución dependería del orden.
    /// </summary>
    private static async Task ExigirClaseSinSolaparAsync(
        DbConnection connection, DbTransaction? tx, long companyId, EvaluacionClaseUpsertDto dto,
        string codigo, int? excepto, CancellationToken ct)
    {
        var duplicada = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(@"
            SELECT EXISTS (SELECT 1 FROM public.prv_evaluacion_clase
                            WHERE company_id = @CompanyId AND codigo = @Codigo
                              AND (@Excepto::integer IS NULL OR id <> @Excepto::integer))",
            new { CompanyId = companyId, Codigo = codigo, Excepto = excepto }, tx, cancellationToken: ct));

        if (duplicada)
        {
            throw new InvalidOperationException($"Ya existe una clase con el código «{codigo}».");
        }

        if (!dto.Activo) return;

        var solapada = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(@"
            SELECT codigo FROM public.prv_evaluacion_clase
             WHERE company_id = @CompanyId AND activo = TRUE
               AND (@Excepto::integer IS NULL OR id <> @Excepto::integer)
               AND puntaje_desde <= @Hasta AND puntaje_hasta >= @Desde
             LIMIT 1",
            new
            {
                CompanyId = companyId,
                Excepto = excepto,
                Desde = dto.PuntajeDesde,
                Hasta = dto.PuntajeHasta
            }, tx, cancellationToken: ct));

        if (!string.IsNullOrWhiteSpace(solapada))
        {
            throw new InvalidOperationException(
                $"El rango {dto.PuntajeDesde:N2}–{dto.PuntajeHasta:N2} se solapa con la clase «{solapada}».");
        }
    }

    private static async Task<EvaluacionCriterioDto?> LeerCriterioAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int id, CancellationToken ct)
        => await connection.QuerySingleOrDefaultAsync<EvaluacionCriterioDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   peso AS Peso, origen AS Origen, metrica AS Metrica, meta AS Meta,
                   parametro AS Parametro, orden AS Orden, activo AS Activo
              FROM public.prv_evaluacion_criterio
             WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

    private static async Task<EvaluacionClaseDto?> LeerClaseAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int id, CancellationToken ct)
        => await connection.QuerySingleOrDefaultAsync<EvaluacionClaseDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   puntaje_desde AS PuntajeDesde, puntaje_hasta AS PuntajeHasta, orden AS Orden
              FROM public.prv_evaluacion_clase
             WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

    private static string DetalleManual(string? usuario, DateTime? fecha, decimal? logro)
        => logro.HasValue && !string.IsNullOrWhiteSpace(usuario)
            ? $"Calificado por {usuario}" + (fecha.HasValue ? $" el {fecha.Value:dd/MM/yyyy}" : string.Empty)
            : "Pendiente de calificar.";

    // ── Acceso a datos ───────────────────────────────────────────────────────

    // Los helpers reciben la transacción de forma explícita: dentro de CalcularAsync/CapturarAsync
    // puede haber una transacción PROPIA (no la ambiente del DbContext), y Npgsql rechaza un
    // comando cuya transacción no sea la activa de la conexión.
    private static async Task<EvaluacionPeriodoDto?> LeerPeriodoAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int periodoId, CancellationToken ct)
    {
        const string sql = @"
            SELECT p.id             AS Id,
                   p.codigo         AS Codigo,
                   p.nombre         AS Nombre,
                   p.fecha_desde    AS FechaDesde,
                   p.fecha_hasta    AS FechaHasta,
                   p.estado         AS Estado,
                   p.fecha_calculo  AS FechaCalculo,
                   p.usuario_calculo AS UsuarioCalculo,
                   p.fecha_cierre   AS FechaCierre,
                   p.usuario_cierre AS UsuarioCierre,
                   (SELECT count(*) FROM public.prv_evaluacion_hdr h
                     WHERE h.company_id = p.company_id AND h.periodo_id = p.id) AS Evaluaciones
              FROM public.prv_evaluacion_periodo p
             WHERE p.company_id = @CompanyId AND p.id = @Id";

        return await connection.QuerySingleOrDefaultAsync<EvaluacionPeriodoDto>(
            new CommandDefinition(sql, new { CompanyId = companyId, Id = periodoId },
                tx, cancellationToken: ct));
    }

    private static async Task<List<EvaluacionCriterioDto>> LeerCriteriosAsync(
        DbConnection connection, DbTransaction? tx, long companyId, CancellationToken ct)
    {
        const string sql = @"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   peso AS Peso, origen AS Origen, metrica AS Metrica, meta AS Meta,
                   parametro AS Parametro, orden AS Orden, activo AS Activo
              FROM public.prv_evaluacion_criterio
             WHERE company_id = @CompanyId AND activo = TRUE
             ORDER BY orden, codigo";

        var filas = await connection.QueryAsync<EvaluacionCriterioDto>(
            new CommandDefinition(sql, new { CompanyId = companyId }, tx, cancellationToken: ct));
        return filas.AsList();
    }

    private static async Task<List<EvaluacionClaseDto>> LeerClasesAsync(
        DbConnection connection, DbTransaction? tx, long companyId, CancellationToken ct)
    {
        const string sql = @"
            SELECT id AS Id, codigo AS Codigo, nombre AS Nombre, descripcion AS Descripcion,
                   puntaje_desde AS PuntajeDesde, puntaje_hasta AS PuntajeHasta, orden AS Orden
              FROM public.prv_evaluacion_clase
             WHERE company_id = @CompanyId AND activo = TRUE
             ORDER BY orden, puntaje_desde DESC";

        var filas = await connection.QueryAsync<EvaluacionClaseDto>(
            new CommandDefinition(sql, new { CompanyId = companyId }, tx, cancellationToken: ct));
        return filas.AsList();
    }

    /// <summary>Una sola pasada a la función de métricas para todo el período.</summary>
    private static async Task<List<MetricaProveedor>> LeerMetricasAsync(
        DbConnection connection, DbTransaction? tx, long companyId, EvaluacionPeriodoDto periodo,
        IReadOnlyList<EvaluacionCriterioDto> criterios, CancellationToken ct)
    {
        // La tolerancia de precio la manda el criterio PRECIO; si no la trae, va el default.
        var tolerancia = ToleranciaPrecioPorDefecto;
        foreach (var c in criterios)
        {
            if (string.Equals(c.Metrica, MetricaEvaluacion.Precio, StringComparison.OrdinalIgnoreCase)
                && c.Parametro.HasValue && c.Parametro.Value > 0m)
            {
                tolerancia = c.Parametro.Value;
                break;
            }
        }

        const string sql = @"
            SELECT cod_proveedor AS CodProveedor,
                   compras       AS Compras,
                   recepciones   AS Recepciones,
                   ordenes       AS Ordenes,
                   entrega_num   AS EntregaNum,   entrega_den   AS EntregaDen,
                   completo_num  AS CompletoNum,  completo_den  AS CompletoDen,
                   precio_num    AS PrecioNum,    precio_den    AS PrecioDen,
                   calidad_num   AS CalidadNum,   calidad_den   AS CalidadDen,
                   documento_num AS DocumentoNum, documento_den AS DocumentoDen
              FROM public.fn_prv_evaluacion_metricas(@CompanyId, @Desde, @Hasta, @Tolerancia)";

        var filas = await connection.QueryAsync<MetricaProveedor>(new CommandDefinition(sql,
            new
            {
                CompanyId = companyId,
                Desde = periodo.FechaDesde,
                Hasta = periodo.FechaHasta,
                Tolerancia = tolerancia
            }, tx, cancellationToken: ct));

        return filas.AsList();
    }

    private static async Task<Dictionary<string, (decimal? Logro, string? Usuario, DateTime? Fecha)>> LeerCapturasAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int periodoId, string codProveedor,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT d.criterio_codigo AS Codigo, d.logro AS Logro,
                   d.usuario_captura AS Usuario, d.fecha_captura AS Fecha
              FROM public.prv_evaluacion_dtl d
              JOIN public.prv_evaluacion_hdr h
                ON h.company_id = d.company_id AND h.id = d.evaluacion_id
             WHERE d.company_id = @CompanyId
               AND h.periodo_id = @PeriodoId
               AND h.cod_proveedor = @Cod
               AND d.origen = @Manual
               AND d.logro IS NOT NULL";

        var filas = await connection.QueryAsync<CapturaManual>(new CommandDefinition(sql,
            new
            {
                CompanyId = companyId,
                PeriodoId = periodoId,
                Cod = codProveedor,
                Manual = OrigenCriterioEvaluacion.Manual
            }, tx, cancellationToken: ct));

        var mapa = new Dictionary<string, (decimal?, string?, DateTime?)>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
        {
            mapa[f.Codigo] = (f.Logro, f.Usuario, f.Fecha);
        }

        return mapa;
    }

    private static async Task<int> GuardarCabeceraAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int periodoId,
        MetricaProveedor m, decimal? puntaje, EvaluacionClaseDto? clase,
        string usuario, DateTime ahora, CancellationToken ct)
    {
        const string sql = @"
            INSERT INTO public.prv_evaluacion_hdr
                   (company_id, periodo_id, cod_proveedor, proveedor_nombre, puntaje, clase_id,
                    clase_codigo, compras_periodo, recepciones, ordenes, estado,
                    usuariocreacion, fechacreacion)
            SELECT @CompanyId, @PeriodoId, @Cod,
                   (SELECT p.nombre FROM public.prv_proveedores p
                     WHERE p.company_id = @CompanyIdInt AND p.cod_proveedor = @Cod),
                   @Puntaje, @ClaseId, @ClaseCodigo, @Compras, @Recepciones, @Ordenes, @Estado,
                   @Usuario, @Ahora
            ON CONFLICT (company_id, periodo_id, cod_proveedor) DO UPDATE
               SET proveedor_nombre    = EXCLUDED.proveedor_nombre,
                   puntaje             = EXCLUDED.puntaje,
                   clase_id            = EXCLUDED.clase_id,
                   clase_codigo        = EXCLUDED.clase_codigo,
                   compras_periodo     = EXCLUDED.compras_periodo,
                   recepciones         = EXCLUDED.recepciones,
                   ordenes             = EXCLUDED.ordenes,
                   usuariomodificacion = @Usuario,
                   fechamodificacion   = @Ahora
            RETURNING id";

        return await connection.ExecuteScalarAsync<int>(new CommandDefinition(sql,
            new
            {
                CompanyId = companyId,
                // prv_proveedores.company_id es int4 y el resto BIGINT: el cast evita el error de tipos.
                CompanyIdInt = (int)companyId,
                PeriodoId = periodoId,
                Cod = m.CodProveedor,
                Puntaje = puntaje,
                ClaseId = clase?.Id,
                ClaseCodigo = clase?.Codigo,
                Compras = m.Compras,
                Recepciones = m.Recepciones,
                Ordenes = m.Ordenes,
                Estado = EstadoEvaluacionProveedor.Calculada,
                Usuario = usuario,
                Ahora = ahora
            }, tx, cancellationToken: ct));
    }

    private static async Task GuardarDetalleAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int evaluacionId,
        List<EvaluacionCriterioResultadoDto> resultados, CancellationToken ct)
    {
        await connection.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.prv_evaluacion_dtl WHERE company_id = @CompanyId AND evaluacion_id = @Id",
            new { CompanyId = companyId, Id = evaluacionId }, tx, cancellationToken: ct));

        const string sql = @"
            INSERT INTO public.prv_evaluacion_dtl
                   (company_id, evaluacion_id, criterio_codigo, criterio_nombre, peso, origen, metrica,
                    numerador, denominador, logro, peso_efectivo, puntos, detalle,
                    usuario_captura, fecha_captura)
            VALUES (@CompanyId, @EvaluacionId, @Codigo, @Nombre, @Peso, @Origen, @Metrica,
                    @Numerador, @Denominador, @Logro, @PesoEfectivo, @Puntos, @Detalle,
                    @UsuarioCaptura, @FechaCaptura)";

        foreach (var r in resultados)
        {
            await connection.ExecuteAsync(new CommandDefinition(sql,
                new
                {
                    CompanyId = companyId,
                    EvaluacionId = evaluacionId,
                    Codigo = r.CriterioCodigo,
                    Nombre = r.CriterioNombre,
                    Peso = r.Peso,
                    Origen = r.Origen,
                    Metrica = r.Metrica,
                    Numerador = r.Numerador,
                    Denominador = r.Denominador,
                    Logro = r.Logro,
                    PesoEfectivo = r.PesoEfectivo,
                    Puntos = r.Puntos,
                    Detalle = r.Detalle,
                    UsuarioCaptura = r.UsuarioCaptura,
                    FechaCaptura = r.FechaCaptura
                }, tx, cancellationToken: ct));
        }
    }

    private static async Task<Dictionary<int, List<EvaluacionCriterioResultadoDto>>> LeerDetallePeriodoAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int periodoId, CancellationToken ct)
    {
        const string sql = @"
            SELECT d.evaluacion_id    AS EvaluacionId,
                   d.criterio_codigo  AS CriterioCodigo,
                   d.criterio_nombre  AS CriterioNombre,
                   d.peso             AS Peso,
                   d.origen           AS Origen,
                   d.metrica          AS Metrica,
                   d.numerador        AS Numerador,
                   d.denominador      AS Denominador,
                   d.logro            AS Logro,
                   d.peso_efectivo    AS PesoEfectivo,
                   d.puntos           AS Puntos,
                   d.detalle          AS Detalle,
                   d.usuario_captura  AS UsuarioCaptura,
                   d.fecha_captura    AS FechaCaptura
              FROM public.prv_evaluacion_dtl d
              JOIN public.prv_evaluacion_hdr h
                ON h.company_id = d.company_id AND h.id = d.evaluacion_id
             WHERE d.company_id = @CompanyId AND h.periodo_id = @PeriodoId
             ORDER BY d.id";

        var filas = await connection.QueryAsync<DetalleFila>(new CommandDefinition(sql,
            new { CompanyId = companyId, PeriodoId = periodoId }, tx, cancellationToken: ct));

        var mapa = new Dictionary<int, List<EvaluacionCriterioResultadoDto>>();
        foreach (var f in filas)
        {
            if (!mapa.TryGetValue(f.EvaluacionId, out var lista))
            {
                lista = new List<EvaluacionCriterioResultadoDto>();
                mapa[f.EvaluacionId] = lista;
            }

            lista.Add(f.ToDto());
        }

        return mapa;
    }

    private static async Task<Dictionary<string, decimal?>> LeerPuntajesPeriodoAnteriorAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int periodoId, CancellationToken ct)
    {
        const string sql = @"
            WITH actual AS (
                SELECT fecha_desde FROM public.prv_evaluacion_periodo
                 WHERE company_id = @CompanyId AND id = @PeriodoId
            ),
            anterior AS (
                SELECT p.id FROM public.prv_evaluacion_periodo p, actual a
                 WHERE p.company_id = @CompanyId AND p.fecha_desde < a.fecha_desde
                 ORDER BY p.fecha_desde DESC
                 LIMIT 1
            )
            SELECT h.cod_proveedor AS Codigo, h.puntaje AS Puntaje
              FROM public.prv_evaluacion_hdr h, anterior an
             WHERE h.company_id = @CompanyId AND h.periodo_id = an.id";

        // Dapper no mapea a ValueTuple con nombres: hace falta un tipo con propiedades.
        var filas = await connection.QueryAsync<PuntajeFila>(
            new CommandDefinition(sql, new { CompanyId = companyId, PeriodoId = periodoId },
                tx, cancellationToken: ct));

        var mapa = new Dictionary<string, decimal?>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas)
        {
            mapa[f.Codigo] = f.Puntaje;
        }

        return mapa;
    }

    private static async Task<EvaluacionFichaDto?> LeerFichaAsync(
        DbConnection connection, DbTransaction? tx, long companyId, int periodoId, string codProveedor,
        CancellationToken ct)
    {
        const string sqlCabecera = @"
            SELECT h.id               AS Id,
                   h.periodo_id       AS PeriodoId,
                   p.codigo           AS PeriodoCodigo,
                   p.nombre           AS PeriodoNombre,
                   p.fecha_desde      AS FechaDesde,
                   p.fecha_hasta      AS FechaHasta,
                   (p.estado = 2)     AS PeriodoCerrado,
                   h.cod_proveedor    AS CodProveedor,
                   h.proveedor_nombre AS ProveedorNombre,
                   pr.rtn             AS Rtn,
                   tp.nombre          AS TipoNombre,
                   h.puntaje          AS Puntaje,
                   h.clase_codigo     AS ClaseCodigo,
                   cl.nombre          AS ClaseNombre,
                   h.compras_periodo  AS ComprasPeriodo,
                   h.recepciones      AS Recepciones,
                   h.ordenes          AS Ordenes,
                   h.estado           AS Estado,
                   h.observaciones    AS Observaciones
              FROM public.prv_evaluacion_hdr h
              JOIN public.prv_evaluacion_periodo p
                ON p.company_id = h.company_id AND p.id = h.periodo_id
              LEFT JOIN public.prv_evaluacion_clase cl
                     ON cl.company_id = h.company_id AND cl.id = h.clase_id
              LEFT JOIN public.prv_proveedores pr
                     ON pr.company_id = @CompanyIdInt AND pr.cod_proveedor = h.cod_proveedor
              LEFT JOIN public.prv_tipoproveedor tp
                     ON tp.cod_tipoproveedor = pr.cod_tipoproveedor
             WHERE h.company_id = @CompanyId AND h.periodo_id = @PeriodoId AND h.cod_proveedor = @Cod";

        var ficha = await connection.QuerySingleOrDefaultAsync<EvaluacionFichaDto>(
            new CommandDefinition(sqlCabecera,
                new
                {
                    CompanyId = companyId,
                    CompanyIdInt = (int)companyId,
                    PeriodoId = periodoId,
                    Cod = codProveedor
                }, tx, cancellationToken: ct));

        if (ficha is null) return null;

        const string sqlDetalle = @"
            SELECT d.evaluacion_id   AS EvaluacionId,
                   d.criterio_codigo AS CriterioCodigo,
                   d.criterio_nombre AS CriterioNombre,
                   d.peso            AS Peso,
                   d.origen          AS Origen,
                   d.metrica         AS Metrica,
                   d.numerador       AS Numerador,
                   d.denominador     AS Denominador,
                   d.logro           AS Logro,
                   d.peso_efectivo   AS PesoEfectivo,
                   d.puntos          AS Puntos,
                   d.detalle         AS Detalle,
                   d.usuario_captura AS UsuarioCaptura,
                   d.fecha_captura   AS FechaCaptura
              FROM public.prv_evaluacion_dtl d
             WHERE d.company_id = @CompanyId AND d.evaluacion_id = @Id
             ORDER BY d.id";

        var detalle = await connection.QueryAsync<DetalleFila>(new CommandDefinition(sqlDetalle,
            new { CompanyId = companyId, Id = ficha.Id }, tx, cancellationToken: ct));
        foreach (var f in detalle)
        {
            ficha.Criterios.Add(f.ToDto());
        }

        const string sqlHistorial = @"
            SELECT p.id          AS PeriodoId,
                   p.codigo      AS PeriodoCodigo,
                   p.fecha_desde AS FechaDesde,
                   h.puntaje     AS Puntaje,
                   h.clase_codigo AS ClaseCodigo
              FROM public.prv_evaluacion_hdr h
              JOIN public.prv_evaluacion_periodo p
                ON p.company_id = h.company_id AND p.id = h.periodo_id
             WHERE h.company_id = @CompanyId AND h.cod_proveedor = @Cod
             ORDER BY p.fecha_desde DESC
             LIMIT 8";

        var historial = await connection.QueryAsync<EvaluacionHistorialItemDto>(
            new CommandDefinition(sqlHistorial, new { CompanyId = companyId, Cod = codProveedor },
                tx, cancellationToken: ct));

        // Del más viejo al más nuevo: así lo dibuja la ficha.
        var lista = historial.AsList();
        for (var i = lista.Count - 1; i >= 0; i--)
        {
            ficha.Historial.Add(lista[i]);
        }

        return ficha;
    }

    // ── Infraestructura ──────────────────────────────────────────────────────

    private long EnsureCompanyId()
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        return companyId;
    }

    private async Task<DbConnection> AbrirConexionAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        return connection;
    }

    /// <summary>
    /// Transacción ambiente del DbContext, si la hay. Los tests envuelven cada prueba en
    /// BEGIN … ROLLBACK: sin pasarla, Dapper abriría su propio ámbito y no vería esos datos.
    /// </summary>
    private DbTransaction? TransaccionActual() => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string Usuario(string? usuario)
        => string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario.Trim();

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    // ── Filas crudas ─────────────────────────────────────────────────────────

    /// <summary>Fila de <c>fn_prv_evaluacion_metricas</c>.</summary>
    private sealed class MetricaProveedor
    {
        public string CodProveedor { get; set; } = string.Empty;
        public decimal Compras { get; set; }
        public int Recepciones { get; set; }
        public int Ordenes { get; set; }
        public decimal EntregaNum { get; set; }
        public decimal EntregaDen { get; set; }
        public decimal CompletoNum { get; set; }
        public decimal CompletoDen { get; set; }
        public decimal PrecioNum { get; set; }
        public decimal PrecioDen { get; set; }
        public decimal CalidadNum { get; set; }
        public decimal CalidadDen { get; set; }
        public decimal DocumentoNum { get; set; }
        public decimal DocumentoDen { get; set; }

        /// <summary>
        /// Numerador, denominador y los dos textos de evidencia de una métrica. El texto de
        /// "sin datos" explica POR QUÉ no hay: es lo que la ficha muestra en vez de un 0%.
        /// </summary>
        public (decimal Num, decimal Den, string ConDatos, string SinDatos) Resolver(string metrica)
            => metrica.ToUpperInvariant() switch
            {
                MetricaEvaluacion.Entrega => (EntregaNum, EntregaDen,
                    $"{EntregaNum:N0} de {EntregaDen:N0} renglones recibidos dentro de la fecha pactada.",
                    "Ninguna recepción del período vino de una orden con fecha de entrega pactada."),

                MetricaEvaluacion.Completo => (CompletoNum, CompletoDen,
                    $"{CompletoNum:N2} de {CompletoDen:N2} unidades pedidas fueron recibidas.",
                    "Ninguna recepción del período vino de una orden de compra."),

                MetricaEvaluacion.Precio => (PrecioNum, PrecioDen,
                    $"{PrecioNum:N0} de {PrecioDen:N0} renglones facturados dentro de la tolerancia.",
                    "Ningún renglón del período se pudo comparar contra el costo de una orden."),

                MetricaEvaluacion.Calidad => (CalidadNum, CalidadDen,
                    $"{CalidadNum:N0} de {CalidadDen:N0} recepciones sin incidencias.",
                    "Todavía no se registran incidencias de recepción: el criterio no puntúa."),

                MetricaEvaluacion.Documento => (DocumentoNum, DocumentoDen,
                    $"{DocumentoNum:N0} de {DocumentoDen:N0} facturas con CAI y número SAR.",
                    "El período no tiene facturas registradas."),

                _ => (0m, 0m, string.Empty, "Métrica desconocida: revise el catálogo de criterios.")
            };
    }

    private sealed class PuntajeFila
    {
        public string Codigo { get; set; } = string.Empty;
        public decimal? Puntaje { get; set; }
    }

    private sealed class CapturaManual
    {
        public string Codigo { get; set; } = string.Empty;
        public decimal? Logro { get; set; }
        public string? Usuario { get; set; }
        public DateTime? Fecha { get; set; }
    }

    private sealed class DetalleFila
    {
        public int EvaluacionId { get; set; }
        public string CriterioCodigo { get; set; } = string.Empty;
        public string CriterioNombre { get; set; } = string.Empty;
        public decimal Peso { get; set; }
        public short Origen { get; set; }
        public string? Metrica { get; set; }
        public decimal? Numerador { get; set; }
        public decimal? Denominador { get; set; }
        public decimal? Logro { get; set; }
        public decimal? PesoEfectivo { get; set; }
        public decimal? Puntos { get; set; }
        public string? Detalle { get; set; }
        public string? UsuarioCaptura { get; set; }
        public DateTime? FechaCaptura { get; set; }

        public EvaluacionCriterioResultadoDto ToDto() => new()
        {
            CriterioCodigo = CriterioCodigo,
            CriterioNombre = CriterioNombre,
            Peso = Peso,
            Origen = Origen,
            Metrica = Metrica,
            Numerador = Numerador,
            Denominador = Denominador,
            Logro = Logro,
            PesoEfectivo = PesoEfectivo,
            Puntos = Puntos,
            Detalle = Detalle,
            UsuarioCaptura = UsuarioCaptura,
            FechaCaptura = FechaCaptura
        };
    }
}
