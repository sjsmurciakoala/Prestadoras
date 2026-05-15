using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Tarifario;

public class PruebaCalculoService : IPruebaCalculoService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public PruebaCalculoService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<PruebaCalculoResultDto> CalcularAsync(PruebaCalculoRequest request, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        const string sql = @"
            SELECT
                company_id              AS ""CompanyId"",
                cliente_id              AS ""ClienteId"",
                cliente_clave           AS ""ClienteClave"",
                cliente_nombre          AS ""ClienteNombre"",
                anio                    AS ""Anio"",
                mes                     AS ""Mes"",
                contador                AS ""Contador"",
                ciclo                   AS ""Ciclo"",
                ruta                    AS ""Ruta"",
                secuencia               AS ""Secuencia"",
                tiene_medidor           AS ""TieneMedidor"",
                condicion_lectura_aplicada AS ""CondicionLecturaAplicada"",
                lectura_anterior        AS ""LecturaAnterior"",
                lectura_actual_efectiva AS ""LecturaActualEfectiva"",
                consumo_facturable      AS ""ConsumoFacturable"",
                numero_factura          AS ""NumeroFactura"",
                id_cai                  AS ""IdCai"",
                correlativo_cai         AS ""CorrelativoCai"",
                fecha_factura           AS ""FechaFactura"",
                fecha_vencimiento       AS ""FechaVencimiento"",
                subtotal_servicios      AS ""SubtotalServicios"",
                subtotal_ajustes        AS ""SubtotalAjustes"",
                saldos_anteriores       AS ""SaldosAnteriores"",
                recargos                AS ""Recargos"",
                total_factura           AS ""TotalFactura"",
                taservi1                AS ""Taservi1"",
                taservi2                AS ""Taservi2"",
                taservi3                AS ""Taservi3"",
                taservi4                AS ""Taservi4"",
                detalle_servicios_json::text AS ""DetalleServiciosJson"",
                warnings_json::text     AS ""WarningsJson"",
                snapshot_contract_version AS ""SnapshotContractVersion""
            FROM public.sp_adm_calcular_factura_lectura(
                @companyId,
                @anio,
                @mes,
                @clienteId,
                NULL::varchar,
                @fechaLectura::date,
                @lecturaActual,
                @condicionLectura::varchar,
                @lecturaPromedio,
                NULL::varchar,
                NULL::varchar,
                NULL::integer,
                NULL::integer,
                NULL::varchar,
                'S'::varchar
            )";

        var conn = _context.Database.GetDbConnection();
        if (conn.State != ConnectionState.Open)
            await conn.OpenAsync(ct);

        var result = await conn.QueryFirstOrDefaultAsync<PruebaCalculoResultDto>(
            new CommandDefinition(sql, new
            {
                companyId,
                anio = request.Anio,
                mes = request.Mes,
                clienteId = request.ClienteId,
                fechaLectura = request.FechaLectura ?? DateTime.Today,
                lecturaActual = request.LecturaActual,
                condicionLectura = request.CondicionLectura ?? "N",
                lecturaPromedio = request.LecturaPromedio
            }, cancellationToken: ct));

        return result ?? throw new InvalidOperationException(
            $"El SP no retornó resultados para cliente_id={request.ClienteId}, anio={request.Anio}, mes={request.Mes}.");
    }
}
