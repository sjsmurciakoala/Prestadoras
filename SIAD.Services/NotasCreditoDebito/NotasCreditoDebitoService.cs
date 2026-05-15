using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.NotasCreditoDebito;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.NotasCreditoDebito;

// Modelo NC/ND V3 (Sprint 3, 2026-05-14). Reemplaza el modelo legacy que
// escribía en `ajustes` / `transaccion_abonado`. Las emisiones delegan en
// los SPs sp_adm_emitir_nota_credito / sp_adm_emitir_nota_debito.
public class NotasCreditoDebitoService : INotasCreditoDebitoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public NotasCreditoDebitoService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    private long CompanyId
    {
        get
        {
            var id = _currentCompanyService.GetCompanyId();
            if (id <= 0)
            {
                throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
            }
            return id;
        }
    }

    private async Task<IDbConnection> OpenConnectionAsync(CancellationToken ct)
    {
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }
        return connection;
    }

    public async Task<IReadOnlyList<NotaClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<NotaClienteLookupDto>();
        }

        var filtro = $"%{query.Trim()}%";
        var companyId = CompanyId;

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.company_id == companyId &&
                (EF.Functions.ILike(c.maestro_cliente_clave, filtro) ||
                 EF.Functions.ILike(c.maestro_cliente_nombre, filtro) ||
                 (c.maestro_cliente_rtn != null && EF.Functions.ILike(c.maestro_cliente_rtn, filtro))))
            .OrderBy(c => c.maestro_cliente_clave)
            .Take(25)
            .Select(c => new NotaClienteLookupDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault(),
                Rtn = c.maestro_cliente_rtn,
                Categoria = c.categoria_servicio != null ? c.categoria_servicio.descripcion : null,
                CicloCodigo = c.ciclos != null ? c.ciclos.ciclos_codigo : null,
                CicloDescripcion = c.ciclos != null ? c.ciclos.ciclos_descripcioncorta : null
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<FacturaOrigenLookupDto>> BuscarFacturasClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return Array.Empty<FacturaOrigenLookupDto>();
        }

        var clave = clienteClave.Trim();
        var companyId = CompanyId;

        return await _context.facturas
            .AsNoTracking()
            .Where(f => f.company_id == companyId && f.clientecodigo == clave)
            .OrderByDescending(f => f.fechaemision)
            .ThenByDescending(f => f.id)
            .Take(50)
            .Select(f => new FacturaOrigenLookupDto
            {
                FacturaId = f.id,
                NumeroFactura = f.numfactura ?? string.Empty,
                FechaEmision = f.fechaemision.HasValue
                    ? f.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                    : (DateTime?)null,
                Periodo = f.periodo,
                SaldoTotal = f.saldototal ?? 0m,
                Estado = f.estado
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MotivoLookupDto>> ListarMotivosAnulacionAsync(CancellationToken ct = default)
    {
        return await _context.cfg_motivo_anulacions
            .AsNoTracking()
            .Where(m => m.activo)
            .OrderBy(m => m.motivo_anulacion_id)
            .Select(m => new MotivoLookupDto
            {
                Id = m.motivo_anulacion_id,
                Codigo = m.codigo,
                Descripcion = m.descripcion
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MotivoLookupDto>> ListarMotivosAumentoAsync(CancellationToken ct = default)
    {
        return await _context.cfg_motivo_aumentos
            .AsNoTracking()
            .Where(m => m.activo)
            .OrderBy(m => m.motivo_aumento_id)
            .Select(m => new MotivoLookupDto
            {
                Id = m.motivo_aumento_id,
                Codigo = m.codigo,
                Descripcion = m.descripcion
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<CaiNotaLookupDto>> ListarCaisNotaAsync(short tipoDocumentoFiscalId, CancellationToken ct = default)
    {
        var companyId = CompanyId;
        var connection = await OpenConnectionAsync(ct);

        const string sql = @"
            SELECT cai_id              AS CaiId,
                   codigo_cai          AS CodigoCai,
                   prefijo_documento   AS PrefijoDocumento,
                   correlativo_actual  AS CorrelativoActual,
                   rango_hasta         AS RangoHasta,
                   tipo_documento_fiscal_id AS TipoDocumentoFiscalId
            FROM public.adm_cai_facturacion
            WHERE company_id = @CompanyId
              AND tipo_documento_fiscal_id = @TipoDoc
              AND status_id = 1
              AND estado_id = 1
              AND current_date >= vigencia_desde
              AND (vigencia_hasta IS NULL OR current_date <= vigencia_hasta)
              AND fecha_limite_emision >= current_date
              AND correlativo_actual < rango_hasta
            ORDER BY vigencia_desde DESC, cai_id DESC";

        var rows = await connection.QueryAsync<CaiNotaLookupDto>(
            new CommandDefinition(sql,
                new { CompanyId = companyId, TipoDoc = tipoDocumentoFiscalId },
                cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<EmitirNotaResponseDto> EmitirNotaCreditoAsync(EmitirNotaCreditoRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = CompanyId;
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var connection = await OpenConnectionAsync(ct);

        const string sql = @"
            SELECT success, codigo, mensaje, nota_credito_id, numero_documento, correlativo
            FROM public.sp_adm_emitir_nota_credito(
                @p_company_id, @p_factura_origen_id, @p_motivo_anulacion_id,
                @p_motivo_detalle, @p_monto_disminuir, @p_lineas::jsonb,
                @p_usuario_emisor, @p_cai_id)";

        try
        {
            var row = await connection.QueryFirstOrDefaultAsync<SpNotaRow>(
                new CommandDefinition(sql, new
                {
                    p_company_id = companyId,
                    p_factura_origen_id = dto.FacturaOrigenId,
                    p_motivo_anulacion_id = dto.MotivoAnulacionId,
                    p_motivo_detalle = (object?)dto.MotivoDetalle ?? DBNull.Value,
                    p_monto_disminuir = (object?)dto.MontoDisminuir ?? DBNull.Value,
                    p_lineas = (object?)null ?? DBNull.Value,
                    p_usuario_emisor = usuario,
                    p_cai_id = dto.CaiId
                }, cancellationToken: ct));

            if (row is null)
            {
                return new EmitirNotaResponseDto { Success = false, Codigo = "SIN_RESULTADO", Mensaje = "El SP no devolvió resultado." };
            }

            return new EmitirNotaResponseDto
            {
                Success = row.success,
                Codigo = row.codigo ?? string.Empty,
                Mensaje = row.mensaje ?? string.Empty,
                NotaId = row.nota_credito_id,
                NumeroDocumento = row.numero_documento ?? string.Empty,
                Correlativo = row.correlativo
            };
        }
        catch (PostgresException ex)
        {
            return new EmitirNotaResponseDto
            {
                Success = false,
                Codigo = "ERROR",
                Mensaje = ex.MessageText
            };
        }
    }

    public async Task<EmitirNotaResponseDto> EmitirNotaDebitoAsync(EmitirNotaDebitoRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = CompanyId;
        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var connection = await OpenConnectionAsync(ct);

        const string sql = @"
            SELECT success, codigo, mensaje, nota_debito_id, numero_documento, correlativo
            FROM public.sp_adm_emitir_nota_debito(
                @p_company_id, @p_factura_origen_id, @p_motivo_aumento_id,
                @p_motivo_detalle, @p_monto_aumentar, @p_lineas::jsonb,
                @p_usuario_emisor, @p_cai_id)";

        try
        {
            var row = await connection.QueryFirstOrDefaultAsync<SpNotaRow>(
                new CommandDefinition(sql, new
                {
                    p_company_id = companyId,
                    p_factura_origen_id = dto.FacturaOrigenId,
                    p_motivo_aumento_id = dto.MotivoAumentoId,
                    p_motivo_detalle = (object?)dto.MotivoDetalle ?? DBNull.Value,
                    p_monto_aumentar = dto.MontoAumentar,
                    p_lineas = (object?)null ?? DBNull.Value,
                    p_usuario_emisor = usuario,
                    p_cai_id = dto.CaiId
                }, cancellationToken: ct));

            if (row is null)
            {
                return new EmitirNotaResponseDto { Success = false, Codigo = "SIN_RESULTADO", Mensaje = "El SP no devolvió resultado." };
            }

            return new EmitirNotaResponseDto
            {
                Success = row.success,
                Codigo = row.codigo ?? string.Empty,
                Mensaje = row.mensaje ?? string.Empty,
                NotaId = row.nota_debito_id,
                NumeroDocumento = row.numero_documento ?? string.Empty,
                Correlativo = row.correlativo
            };
        }
        catch (PostgresException ex)
        {
            return new EmitirNotaResponseDto
            {
                Success = false,
                Codigo = "ERROR",
                Mensaje = ex.MessageText
            };
        }
    }

    public async Task<PagedResult<NotaEmitidaListDto>> ListarNotasEmitidasPagedAsync(
        NotaEmitidaFilterDto filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        var companyId = CompanyId;
        var connection = await OpenConnectionAsync(ct);

        if (skip < 0) skip = 0;
        if (take <= 0) take = 20;

        var search = string.IsNullOrWhiteSpace(filtro.Search) ? null : $"%{filtro.Search.Trim()}%";
        var orderBy = BuildOrderBy(sortField, sortDesc);

        var args = new
        {
            CompanyId = companyId,
            TipoNota = string.IsNullOrWhiteSpace(filtro.TipoNota) ? null : filtro.TipoNota.Trim().ToUpperInvariant(),
            EstadoId = filtro.EstadoId,
            FechaDesde = filtro.FechaDesde,
            FechaHasta = filtro.FechaHasta,
            Search = search,
            Skip = skip,
            Take = take
        };

        const string cte = @"
            WITH notas AS (
                SELECT nc.nota_credito_id   AS NotaId,
                       'NC'                 AS TipoNota,
                       nc.numero_documento  AS NumeroDocumento,
                       nc.fecha_emision     AS FechaEmision,
                       nc.cliente_id        AS ClienteId,
                       nc.razon_social_receptor AS ClienteNombre,
                       nc.factura_origen_numero AS FacturaOrigenNumero,
                       nc.motivo_anulacion_id   AS MotivoId,
                       COALESCE(man.descripcion, '') AS MotivoDescripcion,
                       nc.motivo_detalle    AS MotivoDetalle,
                       nc.monto_disminuir   AS Monto,
                       nc.total_nota        AS TotalNota,
                       nc.estado_id         AS EstadoId,
                       COALESCE(edf.descripcion, '') AS EstadoDescripcion,
                       nc.anula_factura_origen AS AnulaFacturaOrigen,
                       nc.usuario_emisor    AS UsuarioEmisor
                FROM public.adm_nota_credito nc
                LEFT JOIN public.cfg_motivo_anulacion man ON man.motivo_anulacion_id = nc.motivo_anulacion_id
                LEFT JOIN public.cfg_estado_documento_fiscal edf ON edf.estado_id = nc.estado_id
                WHERE nc.company_id = @CompanyId
                UNION ALL
                SELECT nd.nota_debito_id    AS NotaId,
                       'ND'                 AS TipoNota,
                       nd.numero_documento  AS NumeroDocumento,
                       nd.fecha_emision     AS FechaEmision,
                       nd.cliente_id        AS ClienteId,
                       nd.razon_social_receptor AS ClienteNombre,
                       nd.factura_origen_numero AS FacturaOrigenNumero,
                       nd.motivo_aumento_id AS MotivoId,
                       COALESCE(mau.descripcion, '') AS MotivoDescripcion,
                       nd.motivo_detalle    AS MotivoDetalle,
                       nd.monto_aumentar    AS Monto,
                       nd.total_nota        AS TotalNota,
                       nd.estado_id         AS EstadoId,
                       COALESCE(edf2.descripcion, '') AS EstadoDescripcion,
                       false                AS AnulaFacturaOrigen,
                       nd.usuario_emisor    AS UsuarioEmisor
                FROM public.adm_nota_debito nd
                LEFT JOIN public.cfg_motivo_aumento mau ON mau.motivo_aumento_id = nd.motivo_aumento_id
                LEFT JOIN public.cfg_estado_documento_fiscal edf2 ON edf2.estado_id = nd.estado_id
                WHERE nd.company_id = @CompanyId
            )
            SELECT {0} FROM notas
            WHERE (@TipoNota::text IS NULL OR TipoNota = @TipoNota::text)
              AND (@EstadoId::smallint IS NULL OR EstadoId = @EstadoId::smallint)
              AND (@FechaDesde::timestamptz IS NULL OR FechaEmision >= @FechaDesde::timestamptz)
              AND (@FechaHasta::timestamptz IS NULL OR FechaEmision <= @FechaHasta::timestamptz)
              AND (@Search::text IS NULL
                   OR NumeroDocumento ILIKE @Search::text
                   OR FacturaOrigenNumero ILIKE @Search::text
                   OR ClienteNombre ILIKE @Search::text)
            {1}";

        var countSql = string.Format(cte, "COUNT(*)", string.Empty);
        var totalCount = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition(countSql, args, cancellationToken: ct));

        var dataSql = string.Format(cte, "*", $"ORDER BY {orderBy} LIMIT @Take OFFSET @Skip");
        var items = await connection.QueryAsync<NotaEmitidaListDto>(
            new CommandDefinition(dataSql, args, cancellationToken: ct));

        return new PagedResult<NotaEmitidaListDto>(items.ToList(), totalCount);
    }

    private static string BuildOrderBy(string? sortField, bool sortDesc)
    {
        var dir = sortDesc ? "DESC" : "ASC";
        return sortField switch
        {
            nameof(NotaEmitidaListDto.NumeroDocumento) => $"NumeroDocumento {dir}",
            nameof(NotaEmitidaListDto.FechaEmision) => $"FechaEmision {dir}",
            nameof(NotaEmitidaListDto.ClienteNombre) => $"ClienteNombre {dir}",
            nameof(NotaEmitidaListDto.TipoNota) => $"TipoNota {dir}",
            nameof(NotaEmitidaListDto.Monto) => $"Monto {dir}",
            nameof(NotaEmitidaListDto.TotalNota) => $"TotalNota {dir}",
            nameof(NotaEmitidaListDto.EstadoId) => $"EstadoId {dir}",
            _ => "FechaEmision DESC"
        };
    }

    private sealed class SpNotaRow
    {
        public bool success { get; set; }
        public string? codigo { get; set; }
        public string? mensaje { get; set; }
        public long nota_credito_id { get; set; }
        public long nota_debito_id { get; set; }
        public string? numero_documento { get; set; }
        public long correlativo { get; set; }
    }

    // ── Mantenimiento de catálogos de motivos ──

    public async Task<IReadOnlyList<MotivoCrudDto>> ListarMotivosAnulacionCrudAsync(CancellationToken ct = default)
    {
        return await _context.cfg_motivo_anulacions
            .AsNoTracking()
            .OrderBy(m => m.motivo_anulacion_id)
            .Select(m => new MotivoCrudDto
            {
                Id = m.motivo_anulacion_id,
                Codigo = m.codigo,
                Descripcion = m.descripcion,
                Activo = m.activo
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<MotivoCrudDto>> ListarMotivosAumentoCrudAsync(CancellationToken ct = default)
    {
        return await _context.cfg_motivo_aumentos
            .AsNoTracking()
            .OrderBy(m => m.motivo_aumento_id)
            .Select(m => new MotivoCrudDto
            {
                Id = m.motivo_aumento_id,
                Codigo = m.codigo,
                Descripcion = m.descripcion,
                Activo = m.activo
            })
            .ToListAsync(ct);
    }

    public async Task<ResponseModelDto> GuardarMotivoAnulacionAsync(MotivoSaveRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var descripcion = (dto.Descripcion ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(descripcion))
        {
            return ResponseModelDto.Fail("Código y descripción son obligatorios.");
        }

        try
        {
            cfg_motivo_anulacion entity;
            if (dto.Id.HasValue && dto.Id.Value > 0)
            {
                entity = await _context.cfg_motivo_anulacions
                    .FirstOrDefaultAsync(m => m.motivo_anulacion_id == dto.Id.Value, ct)
                    ?? throw new InvalidOperationException("El motivo indicado no existe.");

                if (await _context.cfg_motivo_anulacions
                    .AnyAsync(m => m.codigo == codigo && m.motivo_anulacion_id != dto.Id.Value, ct))
                {
                    return ResponseModelDto.Fail($"Ya existe otro motivo con el código '{codigo}'.");
                }

                entity.codigo = codigo;
                entity.descripcion = descripcion;
                entity.activo = dto.Activo;
            }
            else
            {
                if (await _context.cfg_motivo_anulacions.AnyAsync(m => m.codigo == codigo, ct))
                {
                    return ResponseModelDto.Fail($"Ya existe un motivo con el código '{codigo}'.");
                }

                var maxId = await _context.cfg_motivo_anulacions
                    .MaxAsync(m => (short?)m.motivo_anulacion_id, ct);
                entity = new cfg_motivo_anulacion
                {
                    motivo_anulacion_id = (short)((maxId ?? 0) + 1),
                    codigo = codigo,
                    descripcion = descripcion,
                    aplica_factura = true,
                    aplica_recibo = true,
                    activo = dto.Activo
                };
                _context.cfg_motivo_anulacions.Add(entity);
            }

            await _context.SaveChangesAsync(ct);
            return ResponseModelDto.Ok(new MotivoCrudDto
            {
                Id = entity.motivo_anulacion_id,
                Codigo = entity.codigo,
                Descripcion = entity.descripcion,
                Activo = entity.activo
            }, "Motivo guardado correctamente.");
        }
        catch (Exception ex)
        {
            return ResponseModelDto.Fail($"No se pudo guardar el motivo: {ex.Message}");
        }
    }

    public async Task<ResponseModelDto> GuardarMotivoAumentoAsync(MotivoSaveRequestDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var descripcion = (dto.Descripcion ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(descripcion))
        {
            return ResponseModelDto.Fail("Código y descripción son obligatorios.");
        }

        try
        {
            cfg_motivo_aumento entity;
            if (dto.Id.HasValue && dto.Id.Value > 0)
            {
                entity = await _context.cfg_motivo_aumentos
                    .FirstOrDefaultAsync(m => m.motivo_aumento_id == dto.Id.Value, ct)
                    ?? throw new InvalidOperationException("El motivo indicado no existe.");

                if (await _context.cfg_motivo_aumentos
                    .AnyAsync(m => m.codigo == codigo && m.motivo_aumento_id != dto.Id.Value, ct))
                {
                    return ResponseModelDto.Fail($"Ya existe otro motivo con el código '{codigo}'.");
                }

                entity.codigo = codigo;
                entity.descripcion = descripcion;
                entity.activo = dto.Activo;
            }
            else
            {
                if (await _context.cfg_motivo_aumentos.AnyAsync(m => m.codigo == codigo, ct))
                {
                    return ResponseModelDto.Fail($"Ya existe un motivo con el código '{codigo}'.");
                }

                var maxId = await _context.cfg_motivo_aumentos
                    .MaxAsync(m => (short?)m.motivo_aumento_id, ct);
                entity = new cfg_motivo_aumento
                {
                    motivo_aumento_id = (short)((maxId ?? 0) + 1),
                    codigo = codigo,
                    descripcion = descripcion,
                    activo = dto.Activo
                };
                _context.cfg_motivo_aumentos.Add(entity);
            }

            await _context.SaveChangesAsync(ct);
            return ResponseModelDto.Ok(new MotivoCrudDto
            {
                Id = entity.motivo_aumento_id,
                Codigo = entity.codigo,
                Descripcion = entity.descripcion,
                Activo = entity.activo
            }, "Motivo guardado correctamente.");
        }
        catch (Exception ex)
        {
            return ResponseModelDto.Fail($"No se pudo guardar el motivo: {ex.Message}");
        }
    }
}
