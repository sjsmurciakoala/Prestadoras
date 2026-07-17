using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.AppLectores;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.AppLectores;

/// <summary>
/// Consulta de facturas subidas desde la app de lectores V3. La marca de origen
/// es la fila de <c>adm_cai_correlativo_emitido</c> con <c>lectura_uuid</c> (la app
/// siempre sincroniza con UUID; el portal no lo usa). Se enlaza con la factura,
/// la lectura (<c>historicomedicion</c>) y el catálogo de condiciones. Dapper
/// sobre la conexión del contexto, siempre acotado al tenant de la sesión.
/// </summary>
public sealed class FacturasAppService : IFacturasAppService
{
    private const int MaxRows = 5000;

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public FacturasAppService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    public async Task<IReadOnlyList<FacturaAppListItemDto>> GetAsync(
        FacturaAppFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new FacturaAppFilterDto();

        var where = new List<string>
        {
            "e.company_id = @co",
            "e.lectura_uuid IS NOT NULL",
            "e.factura_id IS NOT NULL",
            "e.status_id = 1",
        };
        var args = new DynamicParameters();
        args.Add("co", _company.GetCompanyId());
        args.Add("limit", MaxRows);

        // factura.ano/mes son int::text para las filas de la app; se compara como
        // texto para no arriesgar un cast sobre datos legacy en el plan de ejecución.
        if (filtro.Anio.HasValue)
        {
            where.Add("f.ano = @anio");
            args.Add("anio", filtro.Anio.Value.ToString());
        }

        if (filtro.Mes.HasValue)
        {
            where.Add("f.mes = @mes");
            args.Add("mes", filtro.Mes.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            where.Add(@"(e.numero_factura ILIKE @search
                OR f.clientecodigo ILIKE @search
                OR cm.maestro_cliente_nombre ILIKE @search
                OR f.usuario ILIKE @search
                OR e.lectura_uuid ILIKE @search)");
            args.Add("search", $"%{filtro.Search.Trim()}%");
        }

        if (!string.IsNullOrWhiteSpace(filtro.Lector))
        {
            where.Add("f.usuario ILIKE @lector");
            args.Add("lector", filtro.Lector.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filtro.Condicion))
        {
            where.Add("hm.condicion = @condicion");
            args.Add("condicion", filtro.Condicion.Trim());
        }

        if (!string.IsNullOrWhiteSpace(filtro.EstadoSync))
        {
            where.Add("e.estado_codigo = @estadoSync");
            args.Add("estadoSync", filtro.EstadoSync.Trim());
        }

        if (filtro.FechaDesde.HasValue)
        {
            where.Add("e.fecha_emision >= @desde");
            args.Add("desde", filtro.FechaDesde.Value.Date);
        }

        if (filtro.FechaHasta.HasValue)
        {
            where.Add("e.fecha_emision < @hasta");
            args.Add("hasta", filtro.FechaHasta.Value.Date.AddDays(1));
        }

        var sql = $@"
            SELECT
                e.factura_id                                        AS FacturaId,
                e.numero_factura                                    AS NumeroFactura,
                COALESCE(f.numrecibo, 0)                            AS NumRecibo,
                COALESCE(f.clientecodigo, '')                       AS ClienteClave,
                COALESCE(cm.maestro_cliente_nombre, '')             AS ClienteNombre,
                NULLIF(BTRIM(COALESCE(f.ano, '')), '')::int         AS Anio,
                NULLIF(BTRIM(COALESCE(f.mes, '')), '')::int         AS Mes,
                f.fechaemision                                      AS FechaEmision,
                f.fechavence                                        AS FechaVence,
                COALESCE(f.saldototal, 0)                           AS Total,
                f.estado                                            AS EstadoFactura,
                f.con_medicion                                      AS ConMedicion,
                f.usuario                                           AS Lector,
                hm.condicion                                        AS Condicion,
                cl.descripcion                                      AS CondicionNombre,
                hm.contador                                         AS Contador,
                hm.lect_ant                                         AS LecturaAnterior,
                hm.lect_act                                         AS LecturaActual,
                hm.consumo                                          AS Consumo,
                hm.fecha_lect_act                                   AS FechaLectura,
                hm.ciclo                                            AS Ciclo,
                hm.ruta                                             AS Ruta,
                hm.secuencia                                        AS Secuencia,
                hm.categoria::text                                  AS Categoria,
                hm.observacion                                      AS Observacion,
                e.lectura_uuid                                      AS LecturaUuid,
                e.estado_codigo                                     AS EstadoSync,
                e.fecha_emision                                     AS FechaSubida,
                e.fecha_confirmacion                                AS FechaConfirmacion,
                e.correlativo                                       AS CorrelativoCai,
                e.detalle_conflicto                                 AS DetalleConflicto
            FROM public.adm_cai_correlativo_emitido e
            JOIN public.factura f
              ON f.company_id = e.company_id AND f.id = e.factura_id
            LEFT JOIN public.cliente_maestro cm
              ON cm.company_id = e.company_id AND cm.maestro_cliente_clave = f.clientecodigo
            LEFT JOIN LATERAL (
                SELECT h.condicion, h.contador, h.lect_ant, h.lect_act, h.consumo,
                       h.fecha_lect_act, h.ciclo, h.ruta, h.secuencia, h.categoria, h.observacion
                FROM public.historicomedicion h
                WHERE h.company_id = e.company_id
                  AND h.clave = f.clientecodigo
                  AND h.numerofactura = e.numero_factura
                ORDER BY h.ide DESC
                LIMIT 1
            ) hm ON TRUE
            LEFT JOIN public.adm_condicion_lectura cl
              ON cl.company_id = e.company_id AND cl.codigo = hm.condicion
            WHERE {string.Join(" AND ", where)}
            ORDER BY e.fecha_emision DESC
            LIMIT @limit";

        var conn = _context.Database.GetDbConnection();
        var rows = await conn.QueryAsync<FacturaAppListItemDto>(new CommandDefinition(sql, args, cancellationToken: ct));
        return rows.ToList();
    }
}
