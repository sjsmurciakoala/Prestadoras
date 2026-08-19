using System.Data;
using System.Data.Common;
using ClosedXML.Excel;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.TalentoHumano;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.TalentoHumano;

/// <summary>
/// Mantenimiento del catálogo de empleados (th_empleado, módulo Talento Humano).
/// <para>
/// Acceso a datos por Dapper (sin LINQ): la tabla no está mapeada en <see cref="SiadDbContext"/>,
/// igual que los catálogos de <c>prv_evaluacion_*</c>. <see cref="ICurrentCompanyService"/> resuelve
/// la empresa porque Dapper no pasa por el filtro global multiempresa.
/// </para>
/// </summary>
public sealed class EmpleadosService : IEmpleadosService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public EmpleadosService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    public async Task<IReadOnlyList<EmpleadoListItemDto>> GetAsync(EmpleadoFilterDto? filtro, CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        filtro ??= new EmpleadoFilterDto();
        var search = string.IsNullOrWhiteSpace(filtro.Search) ? null : $"%{filtro.Search.Trim()}%";

        const string sql = @"
            SELECT e.id AS Id, e.codigo AS Codigo, e.codigo_simafi AS CodigoSimafi, e.nombre AS Nombre,
                   e.identidad AS Identidad, c.nombre AS CargoNombre, d.nombre AS DepartamentoNombre,
                   e.activo AS Activo
              FROM public.th_empleado e
              LEFT JOIN public.th_cargo c        ON c.company_id = e.company_id AND c.id = e.cargo_id
              LEFT JOIN public.th_departamento d ON d.company_id = e.company_id AND d.id = e.departamento_id
             WHERE e.company_id = @CompanyId
               AND (@Activo::boolean IS NULL OR e.activo = @Activo::boolean)
               AND (@Search::text   IS NULL OR e.codigo ILIKE @Search::text OR e.nombre ILIKE @Search::text
                                            OR COALESCE(e.identidad, '')     ILIKE @Search::text
                                            OR COALESCE(e.codigo_simafi, '') ILIKE @Search::text)
             ORDER BY e.nombre";

        var filas = await connection.QueryAsync<EmpleadoListItemDto>(new CommandDefinition(sql,
            new { CompanyId = companyId, Activo = filtro.Activo, Search = search },
            TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<EmpleadoEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        return await connection.QuerySingleOrDefaultAsync<EmpleadoEditDto>(new CommandDefinition(@"
            SELECT id AS Id, codigo AS Codigo, codigo_simafi AS CodigoSimafi, nombre AS Nombre,
                   identidad AS Identidad, cargo_id AS CargoId, departamento_id AS DepartamentoId, activo AS Activo
              FROM public.th_empleado
             WHERE company_id = @CompanyId AND id = @Id",
            new { CompanyId = companyId, Id = id }, TransaccionActual(), cancellationToken: ct));
    }

    public async Task<IReadOnlyList<EmpleadoLookupDto>> GetLookupAsync(CancellationToken ct = default)
    {
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);

        const string sql = @"
            SELECT e.id AS Id, e.codigo AS Codigo, e.nombre AS Nombre,
                   c.nombre AS CargoNombre, d.nombre AS DepartamentoNombre
              FROM public.th_empleado e
              LEFT JOIN public.th_cargo c        ON c.company_id = e.company_id AND c.id = e.cargo_id
              LEFT JOIN public.th_departamento d ON d.company_id = e.company_id AND d.id = e.departamento_id
             WHERE e.company_id = @CompanyId AND e.activo = TRUE
             ORDER BY e.nombre";

        var filas = await connection.QueryAsync<EmpleadoLookupDto>(new CommandDefinition(sql,
            new { CompanyId = companyId }, TransaccionActual(), cancellationToken: ct));

        return filas.AsList();
    }

    public async Task<EmpleadoEditDto> CreateAsync(EmpleadoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var datos = ValidarEmpleado(dto);

        // Selección estricta: si viene un cargo/departamento, tiene que existir en el catálogo de la empresa.
        await ExigirCatalogoAsync(connection, tx, CatalogoTh.Cargo, companyId, dto.CargoId, ct);
        await ExigirCatalogoAsync(connection, tx, CatalogoTh.Departamento, companyId, dto.DepartamentoId, ct);

        // El código interno se AUTOGENERA (correlativo por empresa); el código SIMAFI se deja NULL:
        // solo se puebla por importación Excel, nunca desde el formulario.
        var codigo = await SiguienteCodigoAsync(connection, tx, companyId, ct);

        var id = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
            INSERT INTO public.th_empleado
                   (company_id, codigo, codigo_simafi, nombre, identidad, cargo_id, departamento_id, activo, usuariocreacion, fechacreacion)
            VALUES (@CompanyId, @Codigo, NULL, @Nombre, @Identidad, @CargoId, @DepartamentoId, @Activo, @Usuario, @Ahora)
            RETURNING id",
            new
            {
                CompanyId = companyId,
                Codigo = codigo,
                datos.Nombre,
                datos.Identidad,
                dto.CargoId,
                dto.DepartamentoId,
                dto.Activo,
                Usuario = Usuario(user),
                Ahora = Ahora()
            }, tx, cancellationToken: ct));

        dto.Id = id;
        dto.Codigo = codigo;
        dto.CodigoSimafi = null;
        dto.Nombre = datos.Nombre;
        dto.Identidad = datos.Identidad;
        return dto;
    }

    public async Task<EmpleadoEditDto> UpdateAsync(int id, EmpleadoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var existente = await GetByIdAsync(id, ct) ?? throw new KeyNotFoundException("El empleado no existe.");

        var datos = ValidarEmpleado(dto);

        await ExigirCatalogoAsync(connection, tx, CatalogoTh.Cargo, companyId, dto.CargoId, ct);
        await ExigirCatalogoAsync(connection, tx, CatalogoTh.Departamento, companyId, dto.DepartamentoId, ct);

        // Ni el código interno ni el código SIMAFI se editan: se conservan los guardados.
        await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.th_empleado
               SET nombre = @Nombre, identidad = @Identidad, cargo_id = @CargoId,
                   departamento_id = @DepartamentoId, activo = @Activo,
                   usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id",
            new
            {
                CompanyId = companyId,
                Id = id,
                datos.Nombre,
                datos.Identidad,
                dto.CargoId,
                dto.DepartamentoId,
                dto.Activo,
                Usuario = Usuario(user),
                Ahora = Ahora()
            }, tx, cancellationToken: ct));

        dto.Id = id;
        dto.Codigo = existente.Codigo;
        dto.CodigoSimafi = existente.CodigoSimafi;
        dto.Nombre = datos.Nombre;
        dto.Identidad = datos.Identidad;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();

        var filas = await connection.ExecuteAsync(new CommandDefinition(@"
            UPDATE public.th_empleado
               SET activo = false, usuariomodificacion = @Usuario, fechamodificacion = @Ahora
             WHERE company_id = @CompanyId AND id = @Id AND activo = TRUE",
            new { CompanyId = companyId, Id = id, Usuario = Usuario(user), Ahora = Ahora() },
            tx, cancellationToken: ct));

        if (filas > 0) return true;

        var existe = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM public.th_empleado WHERE company_id = @CompanyId AND id = @Id)",
            new { CompanyId = companyId, Id = id }, tx, cancellationToken: ct));

        return existe; // ya estaba inactivo: idempotente
    }

    public async Task<EmpleadoImportResultDto> ImportarExcelAsync(Stream excelStream, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(excelStream);
        var companyId = EnsureCompanyId();
        var connection = await AbrirConexionAsync(ct);
        var tx = TransaccionActual();
        var usuario = Usuario(user);
        var ahora = Ahora();

        var resultado = new EmpleadoImportResultDto();

        using var workbook = new XLWorkbook(excelStream);
        var ws = workbook.Worksheet(1);
        var rango = ws.RangeUsed();
        if (rango is null)
        {
            resultado.Errores.Add(new EmpleadoImportErrorDto { Fila = 0, Mensaje = "El archivo está vacío." });
            return resultado;
        }

        // El código interno se autogenera para los nuevos: arranco del máximo actual y lo incremento
        // en memoria (toda la importación corre en una sola transacción secuencial).
        var contadorCodigo = await MaxCodigoNumericoAsync(connection, tx, companyId, ct);
        var simafiVistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Cargo/departamento del Excel vienen como texto: se resuelven contra el catálogo (selección
        // estricta). Lo que no exista queda SIN ASIGNAR (NULL), no se crea ni traba la importación.
        var mapaCargos = await CargarMapaCatalogoAsync(connection, tx, CatalogoTh.Cargo, companyId, ct);
        var mapaDeptos = await CargarMapaCatalogoAsync(connection, tx, CatalogoTh.Departamento, companyId, ct);

        var numeroFila = 0;
        foreach (var fila in rango.RowsUsed())
        {
            numeroFila++;
            if (numeroFila == 1) continue; // encabezado

            var codigoSimafi = fila.Cell(1).GetString().Trim();
            var nombre = fila.Cell(2).GetString().Trim();
            var identidad = LimpiarOpcional(fila.Cell(3).GetString(), 20);
            var cargo = LimpiarOpcional(fila.Cell(4).GetString(), 80);
            var departamento = LimpiarOpcional(fila.Cell(5).GetString(), 80);
            var activoTexto = fila.Cell(6).GetString().Trim();

            if (codigoSimafi.Length == 0 && nombre.Length == 0) continue; // fila en blanco: se ignora

            if (codigoSimafi.Length == 0)
            {
                resultado.Errores.Add(new EmpleadoImportErrorDto { Fila = numeroFila, Mensaje = "Falta el código SIMAFI (identifica al empleado en la importación)." });
                continue;
            }
            if (codigoSimafi.Length > 30)
            {
                resultado.Errores.Add(new EmpleadoImportErrorDto { Fila = numeroFila, Mensaje = $"El código SIMAFI «{codigoSimafi}» supera 30 caracteres." });
                continue;
            }
            if (nombre.Length == 0)
            {
                resultado.Errores.Add(new EmpleadoImportErrorDto { Fila = numeroFila, Mensaje = $"El empleado con código SIMAFI «{codigoSimafi}» no tiene nombre." });
                continue;
            }
            if (nombre.Length > 120)
            {
                resultado.Errores.Add(new EmpleadoImportErrorDto { Fila = numeroFila, Mensaje = $"El nombre del empleado «{codigoSimafi}» supera 120 caracteres." });
                continue;
            }
            if (!simafiVistos.Add(codigoSimafi))
            {
                resultado.Errores.Add(new EmpleadoImportErrorDto { Fila = numeroFila, Mensaje = $"El código SIMAFI «{codigoSimafi}» está repetido en el archivo." });
                continue;
            }

            var activo = InterpretarActivo(activoTexto);
            var cargoId = Resolver(mapaCargos, cargo);
            var departamentoId = Resolver(mapaDeptos, departamento);

            var existenteId = await connection.ExecuteScalarAsync<int?>(new CommandDefinition(@"
                SELECT id FROM public.th_empleado WHERE company_id = @CompanyId AND codigo_simafi = @Simafi",
                new { CompanyId = companyId, Simafi = codigoSimafi }, tx, cancellationToken: ct));

            if (existenteId.HasValue)
            {
                // El código interno no se toca; solo se refrescan los datos (por si cambió el nombre, cargo, etc.).
                await connection.ExecuteAsync(new CommandDefinition(@"
                    UPDATE public.th_empleado
                       SET nombre = @Nombre, identidad = @Identidad, cargo_id = @CargoId,
                           departamento_id = @DepartamentoId, activo = @Activo,
                           usuariomodificacion = @Usuario, fechamodificacion = @Ahora
                     WHERE company_id = @CompanyId AND id = @Id",
                    new { CompanyId = companyId, Id = existenteId.Value, Nombre = nombre, Identidad = identidad, CargoId = cargoId, DepartamentoId = departamentoId, Activo = activo, Usuario = usuario, Ahora = ahora },
                    tx, cancellationToken: ct));
                resultado.Actualizados++;
            }
            else
            {
                var codigo = (++contadorCodigo).ToString("D4");
                await connection.ExecuteAsync(new CommandDefinition(@"
                    INSERT INTO public.th_empleado
                           (company_id, codigo, codigo_simafi, nombre, identidad, cargo_id, departamento_id, activo, usuariocreacion, fechacreacion)
                    VALUES (@CompanyId, @Codigo, @Simafi, @Nombre, @Identidad, @CargoId, @DepartamentoId, @Activo, @Usuario, @Ahora)",
                    new { CompanyId = companyId, Codigo = codigo, Simafi = codigoSimafi, Nombre = nombre, Identidad = identidad, CargoId = cargoId, DepartamentoId = departamentoId, Activo = activo, Usuario = usuario, Ahora = ahora },
                    tx, cancellationToken: ct));
                resultado.Insertados++;
            }
        }

        return resultado;
    }

    public byte[] GenerarPlantillaExcel()
    {
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Empleados");

        ws.Cell(1, 1).Value = "CodigoSimafi";
        ws.Cell(1, 2).Value = "Nombre";
        ws.Cell(1, 3).Value = "Identidad";
        ws.Cell(1, 4).Value = "Cargo";
        ws.Cell(1, 5).Value = "Departamento";
        ws.Cell(1, 6).Value = "Activo";
        ws.Row(1).Style.Font.Bold = true;

        ws.Cell(2, 1).Value = "SIMAFI-1001";
        ws.Cell(2, 2).Value = "Juan Pérez";
        ws.Cell(2, 3).Value = "0801-1990-12345";
        ws.Cell(2, 4).Value = "Bodeguero";
        ws.Cell(2, 5).Value = "Almacén";
        ws.Cell(2, 6).Value = "Si";

        ws.Columns(1, 6).AdjustToContents();

        using var ms = new MemoryStream();
        workbook.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Validaciones ─────────────────────────────────────────────────────────

    private static EmpleadoDatos ValidarEmpleado(EmpleadoEditDto dto)
    {
        // El código interno lo asigna el servidor (autogenerado) y no se valida aquí. El cargo y el
        // departamento se validan aparte contra el catálogo (ExigirCatalogoAsync).
        var nombre = (dto.Nombre ?? string.Empty).Trim();
        var identidad = LimpiarOpcional(dto.Identidad, 20);

        if (nombre.Length == 0) throw new InvalidOperationException("El nombre del empleado es obligatorio.");
        if (nombre.Length > 120) throw new InvalidOperationException("El nombre no puede superar 120 caracteres.");
        if ((dto.Identidad ?? string.Empty).Trim().Length > 20) throw new InvalidOperationException("La identidad no puede superar 20 caracteres.");

        return new EmpleadoDatos(nombre, identidad);
    }

    /// <summary>Selección estricta: si el id viene, tiene que ser un cargo/departamento de la empresa. NULL = sin asignar.</summary>
    private static async Task ExigirCatalogoAsync(
        DbConnection connection, DbTransaction? tx, CatalogoTh tipo, long companyId, int? id, CancellationToken ct)
    {
        if (!id.HasValue) return;

        var (tabla, etiqueta) = tipo == CatalogoTh.Cargo ? ("th_cargo", "cargo") : ("th_departamento", "departamento");
        var existe = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT EXISTS (SELECT 1 FROM public.{tabla} WHERE company_id = @CompanyId AND id = @Id)",
            new { CompanyId = companyId, Id = id.Value }, tx, cancellationToken: ct));

        if (!existe) throw new InvalidOperationException($"El {etiqueta} seleccionado no existe en el catálogo.");
    }

    /// <summary>Mapa nombre(lower) → id del catálogo activo de la empresa, para resolver la importación en memoria.</summary>
    private static async Task<Dictionary<string, int>> CargarMapaCatalogoAsync(
        DbConnection connection, DbTransaction? tx, CatalogoTh tipo, long companyId, CancellationToken ct)
    {
        var tabla = tipo == CatalogoTh.Cargo ? "th_cargo" : "th_departamento";
        var filas = await connection.QueryAsync<(int Id, string Nombre)>(new CommandDefinition(
            $"SELECT id, nombre FROM public.{tabla} WHERE company_id = @CompanyId AND activo = TRUE",
            new { CompanyId = companyId }, tx, cancellationToken: ct));

        var mapa = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var f in filas) mapa[f.Nombre.Trim()] = f.Id;
        return mapa;
    }

    /// <summary>Resuelve un texto contra el mapa del catálogo; null si viene vacío o no existe (sin asignar).</summary>
    private static int? Resolver(Dictionary<string, int> mapa, string? texto)
        => !string.IsNullOrWhiteSpace(texto) && mapa.TryGetValue(texto.Trim(), out var id) ? id : null;

    /// <summary>Trim + null si queda vacío; recorta al máximo por si viene de un Excel con datos largos.</summary>
    private static string? LimpiarOpcional(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        return v.Length > max ? v[..max] : v;
    }

    private readonly record struct EmpleadoDatos(string Nombre, string? Identidad);

    /// <summary>Mayor código interno numérico de la empresa (0 si no hay ninguno).</summary>
    private static async Task<long> MaxCodigoNumericoAsync(
        DbConnection connection, DbTransaction? tx, long companyId, CancellationToken ct)
        => await connection.ExecuteScalarAsync<long>(new CommandDefinition(@"
            SELECT COALESCE(MAX(codigo::bigint), 0)
              FROM public.th_empleado
             WHERE company_id = @CompanyId AND codigo ~ '^[0-9]+$'",
            new { CompanyId = companyId }, tx, cancellationToken: ct));

    /// <summary>
    /// Siguiente código interno (correlativo por empresa) con padding a 4 dígitos. Sigue creciendo
    /// más allá de 9999 sin romper el orden porque el máximo se calcula en numérico. El UNIQUE
    /// (company_id, codigo) protege ante una colisión concurrente.
    /// </summary>
    private static async Task<string> SiguienteCodigoAsync(
        DbConnection connection, DbTransaction? tx, long companyId, CancellationToken ct)
        => ((await MaxCodigoNumericoAsync(connection, tx, companyId, ct)) + 1).ToString("D4");

    private static bool InterpretarActivo(string texto)
    {
        if (string.IsNullOrWhiteSpace(texto)) return true;
        var t = texto.Trim().ToUpperInvariant();
        return t is not ("NO" or "N" or "0" or "FALSE" or "INACTIVO");
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

    private DbTransaction? TransaccionActual() => _context.Database.CurrentTransaction?.GetDbTransaction();

    private static string Usuario(string? usuario) => string.IsNullOrWhiteSpace(usuario) ? "sistema" : usuario.Trim();

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
}
