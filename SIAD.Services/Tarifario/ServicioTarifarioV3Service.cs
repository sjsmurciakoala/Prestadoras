using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Tarifario;

public sealed class ServicioTarifarioV3Service : IServicioTarifarioV3Service
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public ServicioTarifarioV3Service(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<ServicioTarifarioV3ListDto>> GetAsync(
        string? search,
        bool? activo,
        bool? facturableApp,
        long? tipoServicioId,
        CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = @"
            SELECT
                s.servicio_id                AS ""ServicioId"",
                s.tipo_servicio_id           AS ""TipoServicioId"",
                ts.codigo                    AS ""TipoServicioCodigo"",
                ts.nombre                    AS ""TipoServicioNombre"",
                s.codigo                     AS ""Codigo"",
                s.nombre                     AS ""Nombre"",
                s.descripcion                AS ""Descripcion"",
                s.es_asignable_cliente       AS ""EsAsignableCliente"",
                s.usa_condicion_medicion     AS ""UsaCondicionMedicion"",
                s.facturable_app             AS ""FacturableApp"",
                s.app_orden                  AS ""AppOrden"",
                s.permite_evento             AS ""PermiteEvento"",
                s.genera_por_regla           AS ""GeneraPorRegla"",
                s.cont_account_id            AS ""CuentaContableId"",
                ca.code                      AS ""CuentaContableCodigo"",
                ca.name                      AS ""CuentaContableNombre"",
                s.orden_visual               AS ""OrdenVisual"",
                s.status_id                  AS ""StatusId"",
                (
                    SELECT COUNT(*)
                    FROM adm_cuadro_tarifario ct
                    WHERE ct.company_id = s.company_id
                      AND ct.servicio_id = s.servicio_id
                )                            AS ""TotalCuadros"",
                (
                    SELECT COUNT(*)
                    FROM adm_cliente_servicio cs
                    WHERE cs.company_id = s.company_id
                      AND cs.servicio_id = s.servicio_id
                      AND cs.status_id = 1
                )                            AS ""TotalClientes"",
                (
                    SELECT COUNT(*)
                    FROM adm_regla_tarifaria rt
                    WHERE rt.company_id = s.company_id
                      AND rt.servicio_referencia_id = s.servicio_id
                      AND rt.status_id = 1
                )                            AS ""TotalReferencias""
            FROM adm_servicio s
            JOIN adm_tipo_servicio ts
              ON ts.company_id = s.company_id
             AND ts.tipo_servicio_id = s.tipo_servicio_id
            LEFT JOIN con_plan_cuentas ca
              ON ca.company_id = s.company_id
             AND ca.account_id = s.cont_account_id
            WHERE s.company_id = @companyId
              AND (@activo IS NULL OR s.status_id = CASE WHEN @activo THEN 1 ELSE 0 END)
              AND (@facturableApp IS NULL OR s.facturable_app = @facturableApp)
              AND (@tipoServicioId IS NULL OR s.tipo_servicio_id = @tipoServicioId)
              AND (
                    @search IS NULL
                    OR s.codigo ILIKE '%' || @search || '%'
                    OR s.nombre ILIKE '%' || @search || '%'
                    OR COALESCE(s.descripcion, '') ILIKE '%' || @search || '%'
                    OR ts.codigo ILIKE '%' || @search || '%'
                  )
            ORDER BY s.status_id DESC, s.orden_visual, s.app_orden, s.codigo;";

        var conn = _context.Database.GetDbConnection();
        var rows = await conn.QueryAsync<ServicioTarifarioV3ListDto>(
            new CommandDefinition(sql, new
            {
                companyId,
                activo,
                facturableApp,
                tipoServicioId,
                search = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
            }, cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<ServicioTarifarioV3EditDto?> GetByIdAsync(long servicioId, CancellationToken ct = default)
    {
        if (servicioId <= 0)
        {
            return null;
        }

        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = @"
            SELECT
                s.servicio_id            AS ""ServicioId"",
                s.tipo_servicio_id       AS ""TipoServicioId"",
                s.codigo                 AS ""Codigo"",
                s.nombre                 AS ""Nombre"",
                s.descripcion            AS ""Descripcion"",
                s.es_asignable_cliente   AS ""EsAsignableCliente"",
                s.usa_condicion_medicion AS ""UsaCondicionMedicion"",
                s.facturable_app         AS ""FacturableApp"",
                s.app_orden              AS ""AppOrden"",
                s.permite_evento         AS ""PermiteEvento"",
                s.genera_por_regla       AS ""GeneraPorRegla"",
                s.cont_account_id        AS ""CuentaContableId"",
                s.orden_visual           AS ""OrdenVisual"",
                (s.status_id = 1)        AS ""Activo""
            FROM adm_servicio s
            WHERE s.company_id = @companyId
              AND s.servicio_id = @servicioId;";

        var conn = _context.Database.GetDbConnection();
        return await conn.QueryFirstOrDefaultAsync<ServicioTarifarioV3EditDto>(
            new CommandDefinition(sql, new { companyId, servicioId }, cancellationToken: ct));
    }

    public async Task<ServicioTarifarioV3CatalogosDto> GetCatalogosAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        const string sqlTipos = @"
            SELECT tipo_servicio_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre""
            FROM adm_tipo_servicio
            WHERE company_id = @companyId
              AND status_id = 1
            ORDER BY codigo;";

        const string sqlCuentas = @"
            SELECT
                account_id AS ""AccountId"",
                code       AS ""Code"",
                name       AS ""Description""
            FROM con_plan_cuentas
            WHERE company_id = @companyId
              AND allows_posting = true
              AND (
                    status IS NULL
                    OR UPPER(status) IN ('ACTIVE', 'ACTIVO')
                  )
            ORDER BY code;";

        var tipos = (await conn.QueryAsync<TipoServicioLookupDto>(
            new CommandDefinition(sqlTipos, new { companyId }, cancellationToken: ct))).ToList();

        var cuentas = (await conn.QueryAsync<CuentaContableLookupDto>(
            new CommandDefinition(sqlCuentas, new { companyId }, cancellationToken: ct))).ToList();

        return new ServicioTarifarioV3CatalogosDto(tipos, cuentas);
    }

    public async Task<ResponseModelDto> GuardarAsync(ServicioTarifarioV3EditDto request, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        var codigo = NormalizeRequired(request.Codigo, 30, "codigo", uppercase: true);
        var nombre = NormalizeRequired(request.Nombre, 150, "nombre");
        var descripcion = NormalizeOptional(request.Descripcion, 300);
        var tipoServicioId = request.TipoServicioId.GetValueOrDefault();
        if (tipoServicioId <= 0)
        {
            return ResponseModelDto.Fail("Seleccione un tipo de servicio.");
        }

        // Enforcement ERSAPS (gap #8): todo servicio facturable debe estar mapeado
        // a una cuenta contable regulatoria (ingresos 5.1.x) antes de poder usarse.
        if (!request.CuentaContableId.HasValue || request.CuentaContableId.Value <= 0)
        {
            return ResponseModelDto.Fail("La cuenta contable regulatoria (ingresos 5.1.x) es obligatoria para el servicio.");
        }

        var cuentaContableId = await ValidateCuentaContableAsync(request.CuentaContableId, ct);
        var statusId = request.Activo ? 1 : 0;

        const string sqlExiste = @"
            SELECT COUNT(*)
            FROM adm_servicio
            WHERE company_id = @companyId
              AND codigo = @codigo
              AND (@servicioId IS NULL OR servicio_id <> @servicioId);";

        var existe = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sqlExiste, new
            {
                companyId,
                codigo,
                servicioId = request.ServicioId
            }, cancellationToken: ct));

        if (existe > 0)
        {
            return ResponseModelDto.Fail($"Ya existe un servicio con el codigo {codigo}.");
        }

        if (request.ServicioId.HasValue && request.ServicioId.Value > 0)
        {
            const string sqlUpdate = @"
                UPDATE adm_servicio
                SET tipo_servicio_id = @tipoServicioId,
                    codigo = @codigo,
                    nombre = @nombre,
                    descripcion = @descripcion,
                    es_asignable_cliente = @esAsignableCliente,
                    usa_condicion_medicion = @usaCondicionMedicion,
                    facturable_app = @facturableApp,
                    app_orden = @appOrden,
                    permite_evento = @permiteEvento,
                    genera_por_regla = @generaPorRegla,
                    cont_account_id = @cuentaContableId,
                    orden_visual = @ordenVisual,
                    status_id = @statusId,
                    updated_at = NOW(),
                    updated_by = @usuario
                WHERE company_id = @companyId
                  AND servicio_id = @servicioId;";

            await conn.ExecuteAsync(new CommandDefinition(sqlUpdate, new
            {
                companyId,
                servicioId = request.ServicioId.Value,
                tipoServicioId,
                codigo,
                nombre,
                descripcion,
                request.EsAsignableCliente,
                request.UsaCondicionMedicion,
                request.FacturableApp,
                request.AppOrden,
                request.PermiteEvento,
                request.GeneraPorRegla,
                cuentaContableId,
                request.OrdenVisual,
                statusId,
                usuario
            }, cancellationToken: ct));

            return ResponseModelDto.Ok(message: "Servicio actualizado.");
        }

        const string sqlInsert = @"
            INSERT INTO adm_servicio
            (
                company_id,
                tipo_servicio_id,
                codigo,
                nombre,
                descripcion,
                es_asignable_cliente,
                usa_condicion_medicion,
                facturable_app,
                app_orden,
                permite_evento,
                genera_por_regla,
                cont_account_id,
                orden_visual,
                status_id,
                created_by
            )
            VALUES
            (
                @companyId,
                @tipoServicioId,
                @codigo,
                @nombre,
                @descripcion,
                @esAsignableCliente,
                @usaCondicionMedicion,
                @facturableApp,
                @appOrden,
                @permiteEvento,
                @generaPorRegla,
                @cuentaContableId,
                @ordenVisual,
                @statusId,
                @usuario
            );";

        await conn.ExecuteAsync(new CommandDefinition(sqlInsert, new
        {
            companyId,
            tipoServicioId,
            codigo,
            nombre,
            descripcion,
            request.EsAsignableCliente,
            request.UsaCondicionMedicion,
            request.FacturableApp,
            request.AppOrden,
            request.PermiteEvento,
            request.GeneraPorRegla,
            cuentaContableId,
            request.OrdenVisual,
            statusId,
            usuario
        }, cancellationToken: ct));

        return ResponseModelDto.Ok(message: "Servicio creado.");
    }

    public async Task<ResponseModelDto> DesactivarAsync(long servicioId, string usuario, CancellationToken ct = default)
    {
        if (servicioId <= 0)
        {
            return ResponseModelDto.Fail("El servicio no es valido.");
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        const string sqlRefs = @"
            SELECT
                (
                    SELECT COUNT(*)
                    FROM adm_cliente_servicio cs
                    WHERE cs.company_id = @companyId
                      AND cs.servicio_id = @servicioId
                      AND cs.status_id = 1
                ) AS clientes,
                (
                    SELECT COUNT(*)
                    FROM adm_cuadro_tarifario ct
                    WHERE ct.company_id = @companyId
                      AND ct.servicio_id = @servicioId
                      AND ct.status_id = 1
                ) AS cuadros,
                (
                    SELECT COUNT(*)
                    FROM adm_regla_tarifaria rt
                    WHERE rt.company_id = @companyId
                      AND rt.servicio_referencia_id = @servicioId
                      AND rt.status_id = 1
                ) AS reglas;";

        var refs = await conn.QueryFirstAsync<ServicioReferenciaStats>(
            new CommandDefinition(sqlRefs, new { companyId, servicioId }, cancellationToken: ct));

        if (refs.Clientes > 0 || refs.Cuadros > 0 || refs.Reglas > 0)
        {
            return ResponseModelDto.Fail(
                $"No se puede desactivar el servicio porque tiene referencias activas: clientes={refs.Clientes}, cuadros={refs.Cuadros}, reglas={refs.Reglas}.");
        }

        const string sql = @"
            UPDATE adm_servicio
            SET status_id = 0,
                updated_at = NOW(),
                updated_by = @usuario
            WHERE company_id = @companyId
              AND servicio_id = @servicioId;";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new { companyId, servicioId, usuario }, cancellationToken: ct));
        return rows > 0
            ? ResponseModelDto.Ok(message: "Servicio desactivado.")
            : ResponseModelDto.Fail("No se encontro el servicio.");
    }

    private async Task<long?> ValidateCuentaContableAsync(long? accountId, CancellationToken ct)
    {
        if (!accountId.HasValue || accountId.Value <= 0)
        {
            return null;
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var cuenta = await _context.con_plan_cuentas
            .AsNoTracking()
            .Where(c => c.company_id == companyId && c.account_id == accountId.Value)
            .Select(c => new { c.account_id, c.allows_posting, c.status })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null)
        {
            throw new InvalidOperationException("La cuenta contable seleccionada no existe.");
        }

        if (!cuenta.allows_posting)
        {
            throw new InvalidOperationException("La cuenta contable seleccionada no permite posteo.");
        }

        var status = (cuenta.status ?? string.Empty).Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(status) && status is not ("ACTIVE" or "ACTIVO"))
        {
            throw new InvalidOperationException("La cuenta contable seleccionada no esta activa.");
        }

        return cuenta.account_id;
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName, bool uppercase = false)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"El campo {fieldName} es requerido.");
        }

        if (normalized.Length > maxLength)
        {
            normalized = normalized[..maxLength];
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
    }

    private sealed class ServicioReferenciaStats
    {
        public int Clientes { get; set; }
        public int Cuadros { get; set; }
        public int Reglas { get; set; }
    }
}
