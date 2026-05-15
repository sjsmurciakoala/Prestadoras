using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Tarifario;

public class ClienteServicioTarifarioService : IClienteServicioTarifarioService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public ClienteServicioTarifarioService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<ClienteServicioItemDto>> GetServiciosClienteAsync(int clienteId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        const string sql = @"
            SELECT
                cs.cliente_servicio_id  AS ClienteServicioId,
                cs.servicio_id          AS ServicioId,
                s.codigo                AS ServicioCodigo,
                s.nombre                AS ServicioNombre,
                cs.categoria_regulatoria_id AS CategoriaRegulatoriaId,
                cr.codigo               AS CategoriaCodigo,
                cr.nombre               AS CategoriaNombre,
                cs.condicion_medicion_id AS CondicionMedicionId,
                cm.codigo               AS CondicionCodigo,
                cm.nombre               AS CondicionNombre,
                cs.segmento_tarifario_id AS SegmentoTarifarioId,
                st.codigo               AS SegmentoCodigo,
                st.nombre               AS SegmentoNombre,
                cs.cuadro_tarifario_id  AS CuadroTarifarioId,
                ct.codigo               AS CuadroCodigo,
                ct.nombre               AS CuadroNombre,
                cs.fecha_alta           AS FechaAlta,
                cs.fecha_baja           AS FechaBaja,
                cs.status_id            AS StatusId
            FROM adm_cliente_servicio cs
            JOIN adm_servicio s       ON s.servicio_id = cs.servicio_id AND s.company_id = cs.company_id
            LEFT JOIN adm_categoria_regulatoria cr ON cr.categoria_regulatoria_id = cs.categoria_regulatoria_id AND cr.company_id = cs.company_id
            LEFT JOIN adm_condicion_medicion cm    ON cm.condicion_medicion_id = cs.condicion_medicion_id AND cm.company_id = cs.company_id
            LEFT JOIN adm_segmento_tarifario st    ON st.segmento_tarifario_id = cs.segmento_tarifario_id AND st.company_id = cs.company_id
            LEFT JOIN adm_cuadro_tarifario ct      ON ct.cuadro_tarifario_id = cs.cuadro_tarifario_id AND ct.company_id = cs.company_id
            WHERE cs.company_id = @companyId
              AND cs.cliente_id = @clienteId
            ORDER BY cs.status_id DESC, s.orden_visual, s.codigo";

        var conn = _context.Database.GetDbConnection();
        var items = await conn.QueryAsync<ClienteServicioItemDto>(
            new CommandDefinition(sql, new { companyId, clienteId }, cancellationToken: ct));

        return items.ToList();
    }

    public async Task<ClienteServicioCatalogosDto> GetCatalogosAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        const string sqlServicios = @"
            SELECT servicio_id AS Id, codigo AS Codigo, nombre AS Nombre
            FROM adm_servicio
            WHERE company_id = @companyId AND status_id = 1 AND es_asignable_cliente = true
            ORDER BY orden_visual, codigo";

        const string sqlCategorias = @"
            SELECT categoria_regulatoria_id AS Id, codigo AS Codigo, nombre AS Nombre
            FROM adm_categoria_regulatoria
            WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        const string sqlCondiciones = @"
            SELECT condicion_medicion_id AS Id, codigo AS Codigo, nombre AS Nombre
            FROM adm_condicion_medicion
            WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        const string sqlSegmentos = @"
            SELECT segmento_tarifario_id AS Id, codigo AS Codigo, nombre AS Nombre, categoria_regulatoria_id AS CategoriaRegulatoriaId
            FROM adm_segmento_tarifario
            WHERE company_id = @companyId AND status_id = 1
            ORDER BY codigo";

        var param = new { companyId };
        var servicios = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlServicios, param, cancellationToken: ct))).ToList();
        var categorias = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlCategorias, param, cancellationToken: ct))).ToList();
        var condiciones = (await conn.QueryAsync<CatalogoLookupDto>(new CommandDefinition(sqlCondiciones, param, cancellationToken: ct))).ToList();
        var segmentos = (await conn.QueryAsync<SegmentoLookupDto>(new CommandDefinition(sqlSegmentos, param, cancellationToken: ct))).ToList();

        return new ClienteServicioCatalogosDto(servicios, categorias, condiciones, segmentos);
    }

    public async Task<ResponseModelDto> GuardarAsync(int clienteId, ClienteServicioSaveRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        using var tx = await conn.BeginTransactionAsync(ct);
        try
        {
            // Resolver cuadro tarifario automáticamente
            long? cuadroId = await ResolverCuadroTarifarioAsync(
                conn, tx, companyId, request.ServicioId,
                request.CategoriaRegulatoriaId, request.CondicionMedicionId,
                request.SegmentoTarifarioId, ct);

            if (request.ClienteServicioId.HasValue && request.ClienteServicioId.Value > 0)
            {
                // UPDATE
                const string sqlUpdate = @"
                    UPDATE adm_cliente_servicio
                    SET servicio_id = @servicioId,
                        categoria_regulatoria_id = @categoriaId,
                        condicion_medicion_id = @condicionId,
                        segmento_tarifario_id = @segmentoId,
                        cuadro_tarifario_id = @cuadroId,
                        fecha_alta = @fechaAlta,
                        updated_at = now(),
                        updated_by = @usuario
                    WHERE cliente_servicio_id = @id
                      AND company_id = @companyId
                      AND cliente_id = @clienteId";

                await conn.ExecuteAsync(new CommandDefinition(sqlUpdate, new
                {
                    id = request.ClienteServicioId.Value,
                    companyId,
                    clienteId,
                    servicioId = request.ServicioId,
                    categoriaId = request.CategoriaRegulatoriaId,
                    condicionId = request.CondicionMedicionId,
                    segmentoId = request.SegmentoTarifarioId,
                    cuadroId,
                    fechaAlta = request.FechaAlta,
                    usuario
                }, transaction: tx, cancellationToken: ct));
            }
            else
            {
                // Verificar duplicado activo
                const string sqlCheck = @"
                    SELECT COUNT(1) FROM adm_cliente_servicio
                    WHERE company_id = @companyId
                      AND cliente_id = @clienteId
                      AND servicio_id = @servicioId
                      AND status_id = 1";

                var existe = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sqlCheck, new
                {
                    companyId, clienteId, servicioId = request.ServicioId
                }, transaction: tx, cancellationToken: ct));

                if (existe > 0)
                {
                    await tx.RollbackAsync(ct);
                    return ResponseModelDto.Fail("El cliente ya tiene asignado ese servicio activo.");
                }

                // INSERT
                const string sqlInsert = @"
                    INSERT INTO adm_cliente_servicio
                        (company_id, cliente_id, servicio_id, categoria_regulatoria_id,
                         condicion_medicion_id, segmento_tarifario_id, cuadro_tarifario_id,
                         fecha_alta, status_id, created_at, created_by)
                    VALUES
                        (@companyId, @clienteId, @servicioId, @categoriaId,
                         @condicionId, @segmentoId, @cuadroId,
                         @fechaAlta, 1, now(), @usuario)";

                await conn.ExecuteAsync(new CommandDefinition(sqlInsert, new
                {
                    companyId,
                    clienteId,
                    servicioId = request.ServicioId,
                    categoriaId = request.CategoriaRegulatoriaId,
                    condicionId = request.CondicionMedicionId,
                    segmentoId = request.SegmentoTarifarioId,
                    cuadroId,
                    fechaAlta = request.FechaAlta,
                    usuario
                }, transaction: tx, cancellationToken: ct));
            }

            await tx.CommitAsync(ct);
            return ResponseModelDto.Ok(message: "Servicio guardado correctamente.");
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<ResponseModelDto> DesactivarAsync(int clienteId, ClienteServicioDesactivarRequest request, string usuario, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        const string sql = @"
            UPDATE adm_cliente_servicio
            SET status_id = 0,
                fecha_baja = CURRENT_DATE,
                updated_at = now(),
                updated_by = @usuario
            WHERE cliente_servicio_id = @id
              AND company_id = @companyId
              AND cliente_id = @clienteId
              AND status_id = 1";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            id = request.ClienteServicioId,
            companyId,
            clienteId,
            usuario
        }, cancellationToken: ct));

        return rows > 0
            ? ResponseModelDto.Ok(message: "Servicio desactivado.")
            : ResponseModelDto.Fail("No se encontró el servicio activo para desactivar.");
    }

    private static async Task<long?> ResolverCuadroTarifarioAsync(
        IDbConnection conn, IDbTransaction tx,
        long companyId, long servicioId,
        long? categoriaId, long? condicionId, long? segmentoId,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT cuadro_tarifario_id
            FROM adm_cuadro_tarifario
            WHERE company_id = @companyId
              AND servicio_id = @servicioId
              AND (categoria_regulatoria_id = @categoriaId OR (@categoriaId IS NULL AND categoria_regulatoria_id IS NULL))
              AND (condicion_medicion_id = @condicionId OR (@condicionId IS NULL AND condicion_medicion_id IS NULL))
              AND (segmento_tarifario_id = @segmentoId OR (@segmentoId IS NULL AND segmento_tarifario_id IS NULL))
              AND status_id = 1
              AND vigencia_desde <= CURRENT_DATE
              AND (vigencia_hasta IS NULL OR vigencia_hasta >= CURRENT_DATE)
            ORDER BY prioridad DESC
            LIMIT 1";

        return await conn.ExecuteScalarAsync<long?>(new CommandDefinition(sql, new
        {
            companyId, servicioId, categoriaId, condicionId, segmentoId
        }, transaction: tx, cancellationToken: ct));
    }
}
