using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Tarifario;

public sealed class CaiTarifarioService : ICaiTarifarioService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public CaiTarifarioService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<CaiFacturacionListDto>> GetCaisAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = @"
            SELECT
                c.cai_id                          AS ""CaiId"",
                c.codigo_cai                      AS ""CodigoCai"",
                c.prefijo_documento               AS ""PrefijoDocumento"",
                c.punto_emision                   AS ""PuntoEmision"",
                c.correlativo_actual              AS ""CorrelativoActual"",
                c.rango_desde                     AS ""RangoDesde"",
                c.rango_hasta                     AS ""RangoHasta"",
                c.vigencia_desde                  AS ""VigenciaDesde"",
                c.vigencia_hasta                  AS ""VigenciaHasta"",
                c.observaciones                   AS ""Observaciones"",
                c.status_id                       AS ""StatusId"",
                c.establecimiento_codigo          AS ""EstablecimientoCodigo"",
                c.tipo_documento_fiscal_id        AS ""TipoDocumentoFiscalId"",
                td.codigo                         AS ""TipoDocumentoFiscalCodigo"",
                td.descripcion                    AS ""TipoDocumentoFiscalDescripcion"",
                c.fecha_limite_emision            AS ""FechaLimiteEmision"",
                c.leyenda_rango                   AS ""LeyendaRango"",
                -- Estado EFECTIVO (ANULADA gana, sino fecha vencida fuerza VENCIDA, sino lo declarado).
                CASE
                    WHEN c.estado_id = 5 THEN 5
                    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 4
                    ELSE c.estado_id
                END                               AS ""EstadoId"",
                CASE
                    WHEN c.estado_id = 5 THEN 'ANULADA'
                    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 'VENCIDA'
                    ELSE est.codigo
                END                               AS ""EstadoCodigo"",
                CASE
                    WHEN c.estado_id = 5 THEN 'Anulada'
                    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 'Vencida'
                    ELSE est.descripcion
                END                               AS ""EstadoDescripcion"",
                (
                    SELECT COUNT(*)::int
                    FROM adm_cai_bloque_reservado b
                    WHERE b.company_id = c.company_id
                      AND b.cai_id = c.cai_id
                      AND b.status_id = 1
                )                                 AS ""TotalBloques"",
                GREATEST(
                    c.correlativo_actual,
                    COALESCE((SELECT MAX(b.correlativo_actual) FROM adm_cai_bloque_reservado b
                              WHERE b.company_id = c.company_id AND b.cai_id = c.cai_id), c.rango_desde - 1)
                ) + 1                              AS ""SiguienteCorrelativoDisponible""
            FROM adm_cai_facturacion c
            LEFT JOIN cfg_tipo_documento_fiscal td
                   ON td.tipo_documento_fiscal_id = c.tipo_documento_fiscal_id
            LEFT JOIN cfg_cai_estado est
                   ON est.cai_estado_id = c.estado_id
            WHERE c.company_id = @companyId
            ORDER BY c.estado_id, c.vigencia_hasta DESC NULLS LAST, c.prefijo_documento;";

        var conn = _context.Database.GetDbConnection();
        if (!await HasRequiredTablesAsync(conn, ct, "adm_cai_facturacion", "adm_cai_bloque_reservado", "cfg_cai_estado"))
        {
            return Array.Empty<CaiFacturacionListDto>();
        }

        var rows = await conn.QueryAsync<CaiFacturacionListDto>(
            new CommandDefinition(sql, new { companyId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<PagedResult<CaiFacturacionListDto>> GetCaisPagedAsync(
        CaiFacturacionFilterDto filter, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        if (!await HasRequiredTablesAsync(conn, ct, "adm_cai_facturacion", "cfg_cai_estado"))
        {
            return new PagedResult<CaiFacturacionListDto>();
        }

        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : Math.Min(take, 500);

        var search = string.IsNullOrWhiteSpace(filter?.Search) ? null : filter.Search.Trim();
        var soloActivo = filter?.Activo;

        var orderClause = (sortField?.ToLowerInvariant()) switch
        {
            "codigocai"            => sortDesc ? "c.codigo_cai DESC"            : "c.codigo_cai",
            "prefijodocumento"     => sortDesc ? "c.prefijo_documento DESC"     : "c.prefijo_documento",
            "vigenciadesde"        => sortDesc ? "c.vigencia_desde DESC"        : "c.vigencia_desde",
            "vigenciahasta"        => sortDesc ? "c.vigencia_hasta DESC NULLS LAST" : "c.vigencia_hasta NULLS FIRST",
            "rangodesde"           => sortDesc ? "c.rango_desde DESC"           : "c.rango_desde",
            "establecimientocodigo" => sortDesc ? "c.establecimiento_codigo DESC" : "c.establecimiento_codigo",
            "estadoid"             => sortDesc ? "c.estado_id DESC"             : "c.estado_id",
            _ => "c.estado_id, c.vigencia_hasta DESC NULLS LAST, c.prefijo_documento"
        };

        var sqlBase = @"
            FROM adm_cai_facturacion c
            LEFT JOIN cfg_tipo_documento_fiscal td
                   ON td.tipo_documento_fiscal_id = c.tipo_documento_fiscal_id
            LEFT JOIN cfg_cai_estado est
                   ON est.cai_estado_id = c.estado_id
            WHERE c.company_id = @companyId
              AND (@search IS NULL
                   OR c.codigo_cai ILIKE '%' || @search || '%'
                   OR c.prefijo_documento ILIKE '%' || @search || '%'
                   OR c.establecimiento_codigo ILIKE '%' || @search || '%')
              AND (@activo IS NULL OR (c.status_id = CASE WHEN @activo THEN 1 ELSE 0 END))
              AND (@estadoId IS NULL OR
                   CASE
                       WHEN c.estado_id = 5 THEN 5
                       WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 4
                       ELSE c.estado_id
                   END = @estadoId)";

        var sqlCount = "SELECT COUNT(*) " + sqlBase;
        var sqlData = @"
            SELECT
                c.cai_id                          AS ""CaiId"",
                c.codigo_cai                      AS ""CodigoCai"",
                c.prefijo_documento               AS ""PrefijoDocumento"",
                c.punto_emision                   AS ""PuntoEmision"",
                c.correlativo_actual              AS ""CorrelativoActual"",
                c.rango_desde                     AS ""RangoDesde"",
                c.rango_hasta                     AS ""RangoHasta"",
                c.vigencia_desde                  AS ""VigenciaDesde"",
                c.vigencia_hasta                  AS ""VigenciaHasta"",
                c.observaciones                   AS ""Observaciones"",
                c.status_id                       AS ""StatusId"",
                c.establecimiento_codigo          AS ""EstablecimientoCodigo"",
                c.tipo_documento_fiscal_id        AS ""TipoDocumentoFiscalId"",
                td.codigo                         AS ""TipoDocumentoFiscalCodigo"",
                td.descripcion                    AS ""TipoDocumentoFiscalDescripcion"",
                c.fecha_limite_emision            AS ""FechaLimiteEmision"",
                c.leyenda_rango                   AS ""LeyendaRango"",
                -- Estado EFECTIVO (ANULADA gana sobre fecha; fecha vencida fuerza VENCIDA).
                CASE
                    WHEN c.estado_id = 5 THEN 5
                    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 4
                    ELSE c.estado_id
                END                               AS ""EstadoId"",
                CASE
                    WHEN c.estado_id = 5 THEN 'ANULADA'
                    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 'VENCIDA'
                    ELSE est.codigo
                END                               AS ""EstadoCodigo"",
                CASE
                    WHEN c.estado_id = 5 THEN 'Anulada'
                    WHEN c.vigencia_hasta IS NOT NULL AND c.vigencia_hasta < CURRENT_DATE THEN 'Vencida'
                    ELSE est.descripcion
                END                               AS ""EstadoDescripcion"",
                (SELECT COUNT(*)::int FROM adm_cai_bloque_reservado b
                 WHERE b.company_id = c.company_id AND b.cai_id = c.cai_id AND b.status_id = 1) AS ""TotalBloques"",
                GREATEST(
                    c.correlativo_actual,
                    COALESCE((SELECT MAX(b.correlativo_actual) FROM adm_cai_bloque_reservado b
                              WHERE b.company_id = c.company_id AND b.cai_id = c.cai_id), c.rango_desde - 1)
                ) + 1 AS ""SiguienteCorrelativoDisponible"""
            + sqlBase + $@"
            ORDER BY {orderClause}
            OFFSET @skip LIMIT @take";

        var parameters = new { companyId, search, activo = soloActivo, estadoId = filter?.EstadoId, skip, take };

        var totalCount = await conn.ExecuteScalarAsync<int>(new CommandDefinition(sqlCount, parameters, cancellationToken: ct));
        var items = (await conn.QueryAsync<CaiFacturacionListDto>(
                          new CommandDefinition(sqlData, parameters, cancellationToken: ct))).ToList();

        return new PagedResult<CaiFacturacionListDto>
        {
            Items = items,
            TotalCount = totalCount
        };
    }

    public async Task<IReadOnlyList<CaiBloqueReservadoListDto>> GetBloquesAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        const string sql = @"
            SELECT
                b.cai_bloque_id                      AS ""CaiBloqueId"",
                b.cai_id                             AS ""CaiId"",
                c.codigo_cai                         AS ""CodigoCai"",
                c.prefijo_documento                  AS ""PrefijoDocumento"",
                b.usuario_asignado                   AS ""UsuarioAsignado"",
                b.dispositivo_id                     AS ""DispositivoId"",
                b.ruta_codigo                        AS ""RutaCodigo"",
                b.correlativo_desde                  AS ""CorrelativoDesde"",
                b.correlativo_hasta                  AS ""CorrelativoHasta"",
                b.correlativo_actual                 AS ""CorrelativoActual"",
                GREATEST(b.correlativo_hasta - b.correlativo_actual, 0) AS ""CorrelativosDisponibles"",
                b.fecha_reserva                      AS ""FechaReserva"",
                b.fecha_expiracion                   AS ""FechaExpiracion"",
                b.estado_codigo                      AS ""EstadoCodigo"",
                b.status_id                          AS ""StatusId""
            FROM adm_cai_bloque_reservado b
            JOIN adm_cai_facturacion c
              ON c.company_id = b.company_id
             AND c.cai_id = b.cai_id
            WHERE b.company_id = @companyId
            ORDER BY b.status_id DESC, b.fecha_reserva DESC, b.cai_bloque_id DESC;";

        var conn = _context.Database.GetDbConnection();
        if (!await HasRequiredTablesAsync(conn, ct, "adm_cai_facturacion", "adm_cai_bloque_reservado"))
        {
            return Array.Empty<CaiBloqueReservadoListDto>();
        }

        var rows = await conn.QueryAsync<CaiBloqueReservadoListDto>(
            new CommandDefinition(sql, new { companyId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ResponseModelDto> GuardarCaiAsync(CaiFacturacionSaveRequest request, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        if (!await HasRequiredTablesAsync(conn, ct, "adm_cai_facturacion"))
        {
            return ResponseModelDto.Fail("La BD no tiene habilitado el core de CAI offline. Ejecuta 20260418_adm_cai_offline_core.sql.");
        }

        var codigoCai = NormalizeRequired(request.CodigoCai, 100, "codigo CAI");
        var observaciones = NormalizeOptional(request.Observaciones, 300);

        // Punto de emisión: 3 dígitos
        var puntoEmision = (request.PuntoEmision ?? "001").Trim().PadLeft(3, '0');
        if (puntoEmision.Length > 3 || !puntoEmision.All(char.IsDigit))
        {
            return ResponseModelDto.Fail("El punto de emisión debe ser numérico de hasta 3 dígitos.");
        }

        if (request.RangoDesde <= 0 || request.RangoHasta <= 0 || request.RangoHasta < request.RangoDesde)
        {
            return ResponseModelDto.Fail("El rango del CAI no es valido.");
        }

        // Establecimiento (EEE): texto libre 3 dígitos, autorizado por SAR
        var establecimientoCod = (request.EstablecimientoCodigo ?? "000").Trim().PadLeft(3, '0');
        if (establecimientoCod.Length > 3 || !establecimientoCod.All(char.IsDigit))
        {
            return ResponseModelDto.Fail("El código de establecimiento (EEE) debe ser numérico de hasta 3 dígitos.");
        }

        if (request.TipoDocumentoFiscalId <= 0)
        {
            return ResponseModelDto.Fail("Debe seleccionar el tipo de documento fiscal que ampara el CAI.");
        }

        // SAR: la fecha límite de emisión = vigencia hasta (mismo concepto, una sola entrada en UI).
        if (request.VigenciaHasta is null)
        {
            return ResponseModelDto.Fail("Debe indicar la fecha límite de emisión (vigencia hasta) — regla SAR.");
        }

        var fechaLimiteEmision = request.VigenciaHasta.Value;

        if (fechaLimiteEmision.Date < request.VigenciaDesde.Date)
        {
            return ResponseModelDto.Fail("La fecha límite de emisión no puede ser anterior a la fecha de solicitud.");
        }

        // Componer prefijo SAR EEE-PPP-TD:
        //   EEE = establecimiento_codigo (texto libre 3 dígitos)
        //   PPP = punto_emision (3 dígitos)
        //   TD  = tipo_documento_fiscal_id (2 dígitos)
        var tipoDocCod = request.TipoDocumentoFiscalId.ToString("D2");
        var prefijo = $"{establecimientoCod}-{puntoEmision}-{tipoDocCod}";

        // Leyenda fiscal SAR Acuerdo 481-2017: tres líneas con CAI, rango autorizado en
        // formato fiscal completo (EEE-PPP-TD-NNNNNNNN) y fecha límite de emisión.
        var rangoDesdeFiscal = $"{prefijo}-{request.RangoDesde:D8}";
        var rangoHastaFiscal = $"{prefijo}-{request.RangoHasta:D8}";
        var leyendaRango = NormalizeOptional(request.LeyendaRango, 200)
            ?? $"CAI: {codigoCai}\nRango autorizado: {rangoDesdeFiscal} al {rangoHastaFiscal}\nFecha límite de emisión: {fechaLimiteEmision:dd/MM/yyyy}";

        // Correlativo actual: si el usuario lo dejó null, asume rango_desde - 1 (inicio limpio).
        // Si lo setea, debe estar dentro del rango.
        var correlativoActual = request.CorrelativoActual ?? (request.RangoDesde - 1);
        if (correlativoActual < request.RangoDesde - 1 || correlativoActual > request.RangoHasta)
        {
            return ResponseModelDto.Fail(
                $"El correlativo actual debe estar entre {request.RangoDesde - 1} y {request.RangoHasta}.");
        }

        const string sqlExiste = @"
            SELECT COUNT(*)
            FROM adm_cai_facturacion
            WHERE company_id = @companyId
              AND codigo_cai = @codigoCai
              AND (@caiId IS NULL OR cai_id <> @caiId);";

        var existe = await conn.ExecuteScalarAsync<int>(
            new CommandDefinition(sqlExiste, new
            {
                companyId,
                codigoCai,
                caiId = request.CaiId
            }, cancellationToken: ct));

        if (existe > 0)
        {
            return ResponseModelDto.Fail("Ya existe un CAI con ese codigo.");
        }

        // Bloqueo en edicion: si el CAI guardado esta Vencido (estado_id=4 o fecha vencida),
        // solo se permite editarlo cuando la nueva fecha limite es hoy o futura (extension).
        if (request.CaiId.HasValue && request.CaiId.Value > 0)
        {
            const string sqlActualEdit = @"
                SELECT vigencia_hasta AS ""VigenciaHasta"", estado_id AS ""EstadoId""
                  FROM adm_cai_facturacion
                 WHERE company_id = @companyId AND cai_id = @caiId;";
            var actual = await conn.QuerySingleOrDefaultAsync<(DateTime? VigenciaHasta, short EstadoId)>(
                new CommandDefinition(sqlActualEdit, new { companyId, caiId = request.CaiId.Value }, cancellationToken: ct));

            var fechaVencidaActual = actual.VigenciaHasta.HasValue && actual.VigenciaHasta.Value.Date < DateTime.Today;
            if ((actual.EstadoId == 4 || fechaVencidaActual)
                && fechaLimiteEmision.Date < DateTime.Today)
            {
                return ResponseModelDto.Fail(
                    "Este CAI esta vencido. Para editarlo debes extender la fecha limite de emision a hoy o una fecha futura.");
            }
        }

        // Estado CAI: 1..5 segun cfg_cai_estado. Reglas de normalizacion al guardar
        // (igual que en SELECT, defensa profunda):
        //   - estado_id = 5 (ANULADA): override manual del operador, gana sobre todo
        //   - vigencia_hasta < hoy   : fuerza estado = 4 (VENCIDA), sin importar lo que envie el user
        //   - resto                  : se respeta lo que mando el cliente (1/2/3)
        // status_id se espeja:
        //   - estado_id = 5  -> status_id = 0 (compat codigo legacy)
        //   - cualquier otro -> status_id = 1
        var estadoSolicitado = request.EstadoId is >= 1 and <= 5 ? request.EstadoId : (short)1;
        short estadoId;
        if (estadoSolicitado == 5)
        {
            estadoId = 5;
        }
        else if (fechaLimiteEmision.Date < DateTime.Today)
        {
            estadoId = 4;
        }
        else
        {
            estadoId = estadoSolicitado;
        }
        var statusId = estadoId == 5 ? 0 : 1;

        if (request.CaiId.HasValue && request.CaiId.Value > 0)
        {
            const string sqlUpdate = @"
                UPDATE adm_cai_facturacion
                SET codigo_cai = @codigoCai,
                    prefijo_documento = @prefijo,
                    punto_emision = @puntoEmision,
                    rango_desde = @rangoDesde,
                    rango_hasta = @rangoHasta,
                    vigencia_desde = @vigenciaDesde,
                    vigencia_hasta = @vigenciaHasta,
                    observaciones = @observaciones,
                    status_id = @statusId,
                    estado_id = @estadoId,
                    establecimiento_codigo = @establecimientoCod,
                    tipo_documento_fiscal_id = @tipoDocumentoFiscalId,
                    fecha_limite_emision = @fechaLimiteEmision,
                    leyenda_rango = @leyendaRango,
                    correlativo_actual = @correlativoActual,
                    updated_at = NOW(),
                    updated_by = @usuario
                WHERE company_id = @companyId
                  AND cai_id = @caiId;";

            await conn.ExecuteAsync(new CommandDefinition(sqlUpdate, new
            {
                companyId,
                caiId = request.CaiId.Value,
                codigoCai,
                prefijo,
                puntoEmision,
                request.RangoDesde,
                request.RangoHasta,
                request.VigenciaDesde,
                request.VigenciaHasta,
                observaciones,
                statusId,
                estadoId,
                establecimientoCod,
                tipoDocumentoFiscalId = request.TipoDocumentoFiscalId,
                fechaLimiteEmision,
                leyendaRango,
                correlativoActual,
                usuario
            }, cancellationToken: ct));

            return ResponseModelDto.Ok(message: "CAI actualizado.");
        }

        const string sqlInsert = @"
            INSERT INTO adm_cai_facturacion
            (
                company_id,
                codigo_cai,
                prefijo_documento,
                punto_emision,
                rango_desde,
                rango_hasta,
                vigencia_desde,
                vigencia_hasta,
                observaciones,
                status_id,
                estado_id,
                establecimiento_codigo,
                tipo_documento_fiscal_id,
                fecha_limite_emision,
                leyenda_rango,
                correlativo_actual,
                created_by
            )
            VALUES
            (
                @companyId,
                @codigoCai,
                @prefijo,
                @puntoEmision,
                @rangoDesde,
                @rangoHasta,
                @vigenciaDesde,
                @vigenciaHasta,
                @observaciones,
                @statusId,
                @estadoId,
                @establecimientoCod,
                @tipoDocumentoFiscalId,
                @fechaLimiteEmision,
                @leyendaRango,
                @correlativoActual,
                @usuario
            );";

        await conn.ExecuteAsync(new CommandDefinition(sqlInsert, new
        {
            companyId,
            codigoCai,
            prefijo,
            puntoEmision,
            request.RangoDesde,
            request.RangoHasta,
            request.VigenciaDesde,
            request.VigenciaHasta,
            observaciones,
            statusId,
            estadoId,
            establecimientoCod,
            tipoDocumentoFiscalId = request.TipoDocumentoFiscalId,
            fechaLimiteEmision,
            leyendaRango,
            correlativoActual,
            usuario
        }, cancellationToken: ct));

        return ResponseModelDto.Ok(message: "CAI creado.");
    }

    public async Task<ResponseModelDto> CambiarEstadoAsync(long caiId, short estadoId, string usuario, CancellationToken ct = default)
    {
        if (estadoId < 1 || estadoId > 5)
        {
            return ResponseModelDto.Fail("Estado fuera de rango (1..5).");
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open) await conn.OpenAsync(ct);

        if (!await HasRequiredTablesAsync(conn, ct, "adm_cai_facturacion", "cfg_cai_estado"))
        {
            return ResponseModelDto.Fail("Falta el script 20260508_cai_estado_lookup.sql.");
        }

        // Reglas:
        //   - CAI ya VENCIDA por fecha (estado_id = 4): es read-only. Cualquier intento
        //     de cambio de estado se rechaza, sin importar el destino solicitado.
        //   - ANULADA (5) gana sobre la fecha (override manual del operador).
        //   - Reactivar (1/2/3) sobre un CAI cuya fecha ya paso -> se fuerza VENCIDA.
        const string sqlActual = @"
            SELECT vigencia_hasta AS ""VigenciaHasta"", estado_id AS ""EstadoId""
              FROM adm_cai_facturacion
             WHERE company_id = @companyId AND cai_id = @caiId;";
        var actual = await conn.QuerySingleOrDefaultAsync<(DateTime? VigenciaHasta, short EstadoId)>(
            new CommandDefinition(sqlActual, new { companyId, caiId }, cancellationToken: ct));

        var fechaVencida = actual.VigenciaHasta.HasValue && actual.VigenciaHasta.Value.Date < DateTime.Today;

        // Bloqueo: ya esta Vencida (ya sea por estado declarado o por fecha) -> no se modifica.
        if (actual.EstadoId == 4 || fechaVencida)
        {
            return ResponseModelDto.Fail(
                "El CAI esta vencido (fecha limite de emision pasada). No se permiten cambios de estado. Para reactivarlo extiende la fecha desde Editar.");
        }

        short estadoFinal;
        if (estadoId == 5)
        {
            estadoFinal = 5;
        }
        else
        {
            estadoFinal = estadoId;
        }

        var statusId = estadoFinal == 5 ? 0 : 1;

        const string sql = @"
            UPDATE adm_cai_facturacion
               SET estado_id  = @estadoFinal,
                   status_id  = @statusId,
                   updated_at = NOW(),
                   updated_by = @usuario
             WHERE company_id = @companyId
               AND cai_id     = @caiId;";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            companyId, caiId, estadoFinal, statusId, usuario
        }, cancellationToken: ct));

        if (rows != 1) return ResponseModelDto.Fail("CAI no encontrado.");

        return ResponseModelDto.Ok(message: "Estado actualizado.");
    }

    public async Task<IReadOnlyList<CaiEstadoLookupDto>> GetCaiEstadosLookupAsync(CancellationToken ct = default)
    {
        var conn = _context.Database.GetDbConnection();
        if (!await HasRequiredTablesAsync(conn, ct, "cfg_cai_estado"))
        {
            return Array.Empty<CaiEstadoLookupDto>();
        }
        const string sql = @"
            SELECT cai_estado_id AS ""CaiEstadoId"",
                   codigo        AS ""Codigo"",
                   descripcion   AS ""Descripcion"",
                   orden         AS ""Orden""
              FROM cfg_cai_estado
             WHERE activo = true
             ORDER BY orden, cai_estado_id;";
        var rows = await conn.QueryAsync<CaiEstadoLookupDto>(new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<TipoDocumentoFiscalLookupDto>> GetTiposDocumentoFiscalLookupAsync(CancellationToken ct = default)
    {
        var conn = _context.Database.GetDbConnection();
        const string sql = @"
            SELECT tipo_documento_fiscal_id AS ""TipoDocumentoFiscalId"",
                   codigo                   AS ""Codigo"",
                   descripcion              AS ""Descripcion"",
                   es_comprobante_fiscal    AS ""EsComprobanteFiscal""
            FROM cfg_tipo_documento_fiscal
            WHERE activo = true
            ORDER BY tipo_documento_fiscal_id;";

        var rows = await conn.QueryAsync<TipoDocumentoFiscalLookupDto>(
            new CommandDefinition(sql, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ResponseModelDto> ReservarBloqueAsync(CaiBloqueReservadoSaveRequest request, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        if (!await HasRequiredTablesAsync(conn, ct, "adm_cai_facturacion", "adm_cai_bloque_reservado"))
        {
            return ResponseModelDto.Fail("La BD no tiene habilitado el core de CAI offline. Ejecuta 20260418_adm_cai_offline_core.sql.");
        }

        if (request.CaiId <= 0)
        {
            return ResponseModelDto.Fail("Seleccione un CAI valido.");
        }

        if (request.CantidadCorrelativos <= 0)
        {
            return ResponseModelDto.Fail("La cantidad de correlativos debe ser mayor que cero.");
        }

        var result = await conn.QueryFirstOrDefaultAsync<BloqueReservadoResult>(
            new CommandDefinition(
                "SELECT * FROM public.sp_adm_reservar_bloque_cai(@p_company_id, @p_cai_id, @p_usuario_asignado, @p_dispositivo_id, @p_ruta_codigo, @p_cantidad, @p_fecha_expiracion, @p_usuario);",
                new
                {
                    p_company_id = companyId,
                    p_cai_id = request.CaiId,
                    p_usuario_asignado = NormalizeOptional(request.UsuarioAsignado, 100),
                    p_dispositivo_id = NormalizeOptional(request.DispositivoId, 100),
                    p_ruta_codigo = NormalizeOptional(request.RutaCodigo, 30),
                    p_cantidad = request.CantidadCorrelativos,
                    p_fecha_expiracion = request.FechaExpiracion,
                    p_usuario = usuario
                },
                cancellationToken: ct));

        return result is not null && result.CaiBloqueId > 0
            ? ResponseModelDto.Ok(
                new
                {
                    result.CaiBloqueId,
                    result.CorrelativoDesde,
                    result.CorrelativoHasta
                },
                $"Bloque reservado: {result.CorrelativoDesde} - {result.CorrelativoHasta}.")
            : ResponseModelDto.Fail("No fue posible reservar el bloque CAI.");
    }

    private static string NormalizeRequired(string? value, int maxLength, string fieldName)
    {
        var normalized = (value ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException($"El campo {fieldName} es requerido.");
        }

        return normalized.Length > maxLength ? normalized[..maxLength] : normalized;
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

    private sealed class BloqueReservadoResult
    {
        public long CaiBloqueId { get; set; }
        public long CorrelativoDesde { get; set; }
        public long CorrelativoHasta { get; set; }
    }
}
