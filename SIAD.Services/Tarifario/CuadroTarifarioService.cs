using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Tarifario;

public class CuadroTarifarioService : ICuadroTarifarioService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public CuadroTarifarioService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<CuadroTarifarioListDto>> GetCuadrosAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        const string sql = @"
            SELECT
                ct.cuadro_tarifario_id  AS ""CuadroTarifarioId"",
                ct.codigo               AS ""Codigo"",
                ct.nombre               AS ""Nombre"",
                ct.descripcion          AS ""Descripcion"",
                ct.servicio_id          AS ""ServicioId"",
                s.codigo                AS ""ServicioCodigo"",
                s.nombre                AS ""ServicioNombre"",
                ct.categoria_regulatoria_id AS ""CategoriaRegulatoriaId"",
                cr.codigo               AS ""CategoriaCodigo"",
                ct.condicion_medicion_id AS ""CondicionMedicionId"",
                cm.codigo               AS ""CondicionCodigo"",
                ct.segmento_tarifario_id AS ""SegmentoTarifarioId"",
                seg.codigo              AS ""SegmentoCodigo"",
                ct.vigencia_desde       AS ""VigenciaDesde"",
                ct.vigencia_hasta       AS ""VigenciaHasta"",
                ct.prioridad            AS ""Prioridad"",
                ct.referencia_normativa AS ""ReferenciaNormativa"",
                ct.status_id            AS ""StatusId"",
                (SELECT COUNT(*) FROM adm_regla_tarifaria rt
                 WHERE rt.cuadro_tarifario_id = ct.cuadro_tarifario_id) AS ""TotalReglas""
            FROM adm_cuadro_tarifario ct
            JOIN adm_servicio s ON s.servicio_id = ct.servicio_id AND s.company_id = ct.company_id
            LEFT JOIN adm_categoria_regulatoria cr ON cr.categoria_regulatoria_id = ct.categoria_regulatoria_id
            LEFT JOIN adm_condicion_medicion cm ON cm.condicion_medicion_id = ct.condicion_medicion_id
            LEFT JOIN adm_segmento_tarifario seg ON seg.segmento_tarifario_id = ct.segmento_tarifario_id
            WHERE ct.company_id = @companyId
            ORDER BY ct.status_id DESC, s.codigo, cr.codigo, cm.codigo, seg.codigo";

        var conn = _context.Database.GetDbConnection();
        var items = await conn.QueryAsync<CuadroTarifarioListDto>(
            new CommandDefinition(sql, new { companyId }, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<CuadroTarifarioCatalogosDto> GetCatalogosAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        const string sqlServicios = @"
            SELECT servicio_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre""
            FROM adm_servicio WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        const string sqlCategorias = @"
            SELECT categoria_regulatoria_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre""
            FROM adm_categoria_regulatoria WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        const string sqlCondiciones = @"
            SELECT condicion_medicion_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre""
            FROM adm_condicion_medicion WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        const string sqlSegmentos = @"
            SELECT segmento_tarifario_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre"",
                   categoria_regulatoria_id AS ""CategoriaRegulatoriaId""
            FROM adm_segmento_tarifario WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        const string sqlTiposRegla = @"
            SELECT tipo_regla_tarifaria_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre""
            FROM adm_tipo_regla_tarifaria WHERE company_id = @companyId
            ORDER BY codigo";

        const string sqlServiciosRef = @"
            SELECT servicio_id AS ""Id"", codigo AS ""Codigo"", nombre AS ""Nombre""
            FROM adm_servicio WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        var p = new { companyId };
        var servicios = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlServicios, p, cancellationToken: ct))).ToList();
        var categorias = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlCategorias, p, cancellationToken: ct))).ToList();
        var condiciones = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlCondiciones, p, cancellationToken: ct))).ToList();
        var segmentos = (await conn.QueryAsync<SegmentoLookupDto>(new CommandDefinition(sqlSegmentos, p, cancellationToken: ct))).ToList();
        var tiposRegla = (await conn.QueryAsync<TipoReglaTarifariaLookupDto>(new CommandDefinition(sqlTiposRegla, p, cancellationToken: ct))).ToList();
        var serviciosRef = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlServiciosRef, p, cancellationToken: ct))).ToList();

        return new CuadroTarifarioCatalogosDto(servicios, categorias, condiciones, segmentos, tiposRegla, serviciosRef);
    }

    public async Task<ResponseModelDto> GuardarCuadroAsync(CuadroTarifarioSaveRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        if (request.CuadroTarifarioId.HasValue)
        {
            const string sqlUpdate = @"
                UPDATE adm_cuadro_tarifario SET
                    servicio_id = @servicioId,
                    categoria_regulatoria_id = @categoriaId,
                    condicion_medicion_id = @condicionId,
                    segmento_tarifario_id = @segmentoId,
                    codigo = @codigo,
                    nombre = @nombre,
                    descripcion = @descripcion,
                    vigencia_desde = @vigenciaDesde,
                    vigencia_hasta = @vigenciaHasta,
                    prioridad = @prioridad,
                    referencia_normativa = @referenciaNormativa,
                    updated_at = NOW(), updated_by = @usuario
                WHERE cuadro_tarifario_id = @id AND company_id = @companyId";

            await conn.ExecuteAsync(new CommandDefinition(sqlUpdate, new
            {
                id = request.CuadroTarifarioId.Value,
                companyId,
                servicioId = request.ServicioId,
                categoriaId = request.CategoriaRegulatoriaId,
                condicionId = request.CondicionMedicionId,
                segmentoId = request.SegmentoTarifarioId,
                codigo = request.Codigo,
                nombre = request.Nombre,
                descripcion = request.Descripcion,
                vigenciaDesde = request.VigenciaDesde,
                vigenciaHasta = request.VigenciaHasta,
                prioridad = request.Prioridad,
                referenciaNormativa = request.ReferenciaNormativa,
                usuario
            }, cancellationToken: ct));

            return ResponseModelDto.Ok(message: "Cuadro actualizado.");
        }
        else
        {
            // cuadro_tarifario_id es identity: lo genera la base (la secuencia
            // adm_cuadro_tarifario_seq nunca existió; era un nombre inventado).
            const string sqlInsert = @"
                INSERT INTO adm_cuadro_tarifario (
                    company_id, servicio_id,
                    categoria_regulatoria_id, condicion_medicion_id, segmento_tarifario_id,
                    codigo, nombre, descripcion,
                    vigencia_desde, vigencia_hasta, prioridad, referencia_normativa,
                    status_id, created_by
                ) VALUES (
                    @companyId, @servicioId,
                    @categoriaId, @condicionId, @segmentoId,
                    @codigo, @nombre, @descripcion,
                    @vigenciaDesde, @vigenciaHasta, @prioridad, @referenciaNormativa,
                    1, @usuario
                )";

            await conn.ExecuteAsync(new CommandDefinition(sqlInsert, new
            {
                companyId,
                servicioId = request.ServicioId,
                categoriaId = request.CategoriaRegulatoriaId,
                condicionId = request.CondicionMedicionId,
                segmentoId = request.SegmentoTarifarioId,
                codigo = request.Codigo,
                nombre = request.Nombre,
                descripcion = request.Descripcion,
                vigenciaDesde = request.VigenciaDesde,
                vigenciaHasta = request.VigenciaHasta,
                prioridad = request.Prioridad,
                referenciaNormativa = request.ReferenciaNormativa,
                usuario
            }, cancellationToken: ct));

            return ResponseModelDto.Ok(message: "Cuadro creado.");
        }
    }

    public async Task<ResponseModelDto> DesactivarCuadroAsync(long cuadroTarifarioId, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE adm_cuadro_tarifario
            SET status_id = 0, updated_at = NOW(), updated_by = @usuario
            WHERE cuadro_tarifario_id = @id AND company_id = @companyId";

        await conn.ExecuteAsync(new CommandDefinition(sql, new { id = cuadroTarifarioId, companyId, usuario }, cancellationToken: ct));
        return ResponseModelDto.Ok(message: "Cuadro desactivado.");
    }

    // ── Reglas ──

    public async Task<IReadOnlyList<ReglaTarifariaListDto>> GetReglasAsync(long cuadroTarifarioId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        const string sql = @"
            SELECT
                rt.regla_tarifaria_id       AS ""ReglaTarifariaId"",
                rt.cuadro_tarifario_id      AS ""CuadroTarifarioId"",
                rt.tipo_regla_tarifaria_id  AS ""TipoReglaTarifariaId"",
                trt.codigo                  AS ""TipoReglaCodigo"",
                trt.nombre                  AS ""TipoReglaNombre"",
                rt.orden                    AS ""Orden"",
                rt.consumo_minimo           AS ""ConsumoMinimo"",
                rt.consumo_maximo           AS ""ConsumoMaximo"",
                rt.monto_fijo               AS ""MontoFijo"",
                rt.monto_unitario           AS ""MontoUnitario"",
                rt.porcentaje               AS ""Porcentaje"",
                rt.servicio_referencia_id   AS ""ServicioReferenciaId"",
                sref.codigo                 AS ""ServicioReferenciaCodigo"",
                rt.parametros::text         AS ""Parametros"",
                rt.status_id                AS ""StatusId""
            FROM adm_regla_tarifaria rt
            JOIN adm_tipo_regla_tarifaria trt
              ON trt.tipo_regla_tarifaria_id = rt.tipo_regla_tarifaria_id
            LEFT JOIN adm_servicio sref
              ON sref.servicio_id = rt.servicio_referencia_id AND sref.company_id = rt.company_id
            WHERE rt.cuadro_tarifario_id = @cuadroTarifarioId
              AND rt.company_id = @companyId
            ORDER BY rt.orden, rt.regla_tarifaria_id";

        var conn = _context.Database.GetDbConnection();
        var items = await conn.QueryAsync<ReglaTarifariaListDto>(
            new CommandDefinition(sql, new { cuadroTarifarioId, companyId }, cancellationToken: ct));
        return items.ToList();
    }

    public async Task<ResponseModelDto> GuardarReglaAsync(ReglaTarifariaSaveRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        if (request.ReglaTarifariaId.HasValue)
        {
            const string sqlUpdate = @"
                UPDATE adm_regla_tarifaria SET
                    tipo_regla_tarifaria_id = @tipoReglaId,
                    orden = @orden,
                    consumo_minimo = @consumoMin,
                    consumo_maximo = @consumoMax,
                    monto_fijo = @montoFijo,
                    monto_unitario = @montoUnitario,
                    porcentaje = @porcentaje,
                    servicio_referencia_id = @servicioRefId,
                    parametros = @parametros::jsonb,
                    updated_at = NOW(), updated_by = @usuario
                WHERE regla_tarifaria_id = @id AND company_id = @companyId";

            await conn.ExecuteAsync(new CommandDefinition(sqlUpdate, new
            {
                id = request.ReglaTarifariaId.Value,
                companyId,
                tipoReglaId = request.TipoReglaTarifariaId,
                orden = request.Orden,
                consumoMin = request.ConsumoMinimo,
                consumoMax = request.ConsumoMaximo,
                montoFijo = request.MontoFijo,
                montoUnitario = request.MontoUnitario,
                porcentaje = request.Porcentaje,
                servicioRefId = request.ServicioReferenciaId,
                parametros = request.Parametros,
                usuario
            }, cancellationToken: ct));

            return ResponseModelDto.Ok(message: "Regla actualizada.");
        }
        else
        {
            const string sqlInsert = @"
                INSERT INTO adm_regla_tarifaria (
                    regla_tarifaria_id, company_id, cuadro_tarifario_id,
                    tipo_regla_tarifaria_id, orden,
                    consumo_minimo, consumo_maximo,
                    monto_fijo, monto_unitario, porcentaje,
                    servicio_referencia_id, parametros,
                    status_id, created_by
                ) VALUES (
                    nextval('adm_regla_tarifaria_seq'), @companyId, @cuadroId,
                    @tipoReglaId, @orden,
                    @consumoMin, @consumoMax,
                    @montoFijo, @montoUnitario, @porcentaje,
                    @servicioRefId, @parametros::jsonb,
                    1, @usuario
                )";

            await conn.ExecuteAsync(new CommandDefinition(sqlInsert, new
            {
                companyId,
                cuadroId = request.CuadroTarifarioId,
                tipoReglaId = request.TipoReglaTarifariaId,
                orden = request.Orden,
                consumoMin = request.ConsumoMinimo,
                consumoMax = request.ConsumoMaximo,
                montoFijo = request.MontoFijo,
                montoUnitario = request.MontoUnitario,
                porcentaje = request.Porcentaje,
                servicioRefId = request.ServicioReferenciaId,
                parametros = request.Parametros,
                usuario
            }, cancellationToken: ct));

            return ResponseModelDto.Ok(message: "Regla creada.");
        }
    }

    public async Task<ResponseModelDto> EliminarReglaAsync(long reglaTarifariaId, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        const string sql = @"
            UPDATE adm_regla_tarifaria
            SET status_id = 0, updated_at = NOW(), updated_by = @usuario
            WHERE regla_tarifaria_id = @id AND company_id = @companyId";

        await conn.ExecuteAsync(new CommandDefinition(sql, new { id = reglaTarifariaId, companyId, usuario }, cancellationToken: ct));
        return ResponseModelDto.Ok(message: "Regla eliminada.");
    }
}
