using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Tarifario;

public sealed class TarifarioConflictoService : ITarifarioConflictoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public TarifarioConflictoService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<TarifarioConflictoListDto>> GetAsync(
        string? search,
        string? estadoCodigo,
        string? rutaCodigo,
        int? clienteId,
        CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = @"
            SELECT
                c.lectura_v3_conflicto_sync_id  AS ""ConflictoId"",
                c.created_at                    AS ""FechaRegistro"",
                c.estado_codigo                 AS ""EstadoCodigo"",
                c.codigo_conflicto              AS ""CodigoConflicto"",
                c.cliente_id                    AS ""ClienteId"",
                c.cliente_clave                 AS ""ClienteClave"",
                COALESCE(cm.maestro_cliente_nombre, c.cliente_clave, '(sin cliente)') AS ""ClienteNombre"",
                c.lectura_uuid                  AS ""LecturaUuid"",
                c.cai_id                        AS ""CaiId"",
                c.cai_bloque_id                 AS ""CaiBloqueId"",
                cf.codigo_cai                   AS ""CodigoCai"",
                cf.prefijo_documento            AS ""PrefijoDocumento"",
                b.ruta_codigo                   AS ""RutaCodigo"",
                b.usuario_asignado              AS ""UsuarioAsignado"",
                b.dispositivo_id                AS ""DispositivoId"",
                c.correlativo                   AS ""Correlativo"",
                c.numero_factura                AS ""NumeroFactura"",
                c.detalle_conflicto             AS ""DetalleConflicto"",
                c.factura_id                    AS ""FacturaId""
            FROM adm_lectura_v3_conflicto_sync c
            LEFT JOIN cliente_maestro cm
              ON cm.company_id = c.company_id
             AND cm.maestro_cliente_id = c.cliente_id
            LEFT JOIN adm_cai_facturacion cf
              ON cf.company_id = c.company_id
             AND cf.cai_id = c.cai_id
            LEFT JOIN adm_cai_bloque_reservado b
              ON b.company_id = c.company_id
             AND b.cai_bloque_id = c.cai_bloque_id
            WHERE c.company_id = @companyId
              AND (@clienteId IS NULL OR c.cliente_id = @clienteId)
              AND (@estadoCodigo IS NULL OR c.estado_codigo = @estadoCodigo)
              AND (@rutaCodigo IS NULL OR b.ruta_codigo = @rutaCodigo)
              AND (
                    @search IS NULL
                    OR c.numero_factura ILIKE '%' || @search || '%'
                    OR COALESCE(c.cliente_clave, '') ILIKE '%' || @search || '%'
                    OR COALESCE(cm.maestro_cliente_nombre, '') ILIKE '%' || @search || '%'
                    OR COALESCE(c.codigo_conflicto, '') ILIKE '%' || @search || '%'
                    OR COALESCE(c.detalle_conflicto, '') ILIKE '%' || @search || '%'
                  )
            ORDER BY
                CASE WHEN c.estado_codigo = 'PENDIENTE' THEN 0 ELSE 1 END,
                c.created_at DESC,
                c.lectura_v3_conflicto_sync_id DESC;";

        var conn = _context.Database.GetDbConnection();
        if (!await HasRequiredTablesAsync(conn, ct, "adm_lectura_v3_conflicto_sync"))
        {
            return Array.Empty<TarifarioConflictoListDto>();
        }

        var rows = await conn.QueryAsync<TarifarioConflictoListDto>(
            new CommandDefinition(sql, new
            {
                companyId,
                clienteId,
                estadoCodigo = NormalizeOptional(estadoCodigo),
                rutaCodigo = NormalizeOptional(rutaCodigo),
                search = NormalizeOptional(search)
            }, cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<ResponseModelDto> ResolverAsync(
        TarifarioConflictoResolveRequest request,
        string usuario,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.ConflictoId <= 0)
        {
            return ResponseModelDto.Fail("Seleccione un conflicto valido.");
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        if (!await HasRequiredTablesAsync(conn, ct, "adm_lectura_v3_conflicto_sync"))
        {
            return ResponseModelDto.Fail("La BD no tiene habilitada la bitacora de conflictos. Ejecuta 20260419_v3_cai_sync_conflictos.sql.");
        }

        const string sql = @"
            UPDATE adm_lectura_v3_conflicto_sync
            SET estado_codigo = 'REVISADO',
                detalle_conflicto = CASE
                    WHEN @observaciones IS NULL THEN detalle_conflicto
                    WHEN detalle_conflicto IS NULL OR BTRIM(detalle_conflicto) = '' THEN @observaciones
                    ELSE detalle_conflicto || E'\n---\n' || @observaciones
                END,
                updated_at = now(),
                updated_by = @usuario
            WHERE company_id = @companyId
              AND lectura_v3_conflicto_sync_id = @conflictoId;";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            companyId,
            conflictoId = request.ConflictoId,
            observaciones = NormalizeOptional(request.Observaciones),
            usuario
        }, cancellationToken: ct));

        return rows > 0
            ? ResponseModelDto.Ok(message: "Conflicto marcado como revisado.")
            : ResponseModelDto.Fail("No se encontro el conflicto seleccionado.");
    }

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task<bool> HasRequiredTablesAsync(IDbConnection conn, CancellationToken ct, params string[] tableNames)
    {
        const string sql = @"
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public'
                  AND table_name = @tableName
            );";

        foreach (var tableName in tableNames)
        {
            var exists = await conn.ExecuteScalarAsync<bool>(
                new CommandDefinition(sql, new { tableName }, cancellationToken: ct));
            if (!exists)
            {
                return false;
            }
        }

        return true;
    }
}
