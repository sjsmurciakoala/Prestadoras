using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Tarifario;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Services.Clientes;

namespace SIAD.Services.Tarifario;

public sealed class DesgloseAbonoConfigService : IDesgloseAbonoConfigService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public DesgloseAbonoConfigService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    // Los ítems configurables son los mismos del desglose del estado de cuenta:
    // servicios activos del catálogo más el ítem especial "Saldo anterior".
    private const string SqlItems = @"
        SELECT i.item_codigo                AS ""ItemCodigo"",
               i.item_nombre                AS ""ItemNombre"",
               i.orden                      AS ""Orden"",
               i.es_catalogo                AS ""EsServicioCatalogo"",
               COALESCE(p.porcentaje, 0)    AS ""Porcentaje""
        FROM (
            SELECT s.codigo                         AS item_codigo,
                   s.nombre                         AS item_nombre,
                   COALESCE(s.orden_visual, 0)::int AS orden,
                   TRUE                             AS es_catalogo
            FROM adm_servicio s
            WHERE s.company_id = @companyId
              AND s.status_id  = 1
            UNION ALL
            SELECT @saldoAnterior, 'Saldo anterior', 9000, FALSE
        ) i
        LEFT JOIN public.adm_desglose_abono_porcentaje p
               ON p.company_id  = @companyId
              AND p.item_codigo = i.item_codigo
        ORDER BY i.orden, i.item_nombre;";

    public async Task<IReadOnlyList<DesgloseAbonoItemDto>> GetAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        var rows = await conn.QueryAsync<DesgloseAbonoItemDto>(
            new CommandDefinition(SqlItems,
                new { companyId, saldoAnterior = DesgloseAbonoDistribuidor.CodigoSaldoAnterior },
                cancellationToken: ct));

        return rows.ToList();
    }

    public async Task<ResponseModelDto> GuardarAsync(DesgloseAbonoGuardarDto dto, string usuario, CancellationToken ct = default)
    {
        if (dto?.Items is null)
        {
            return ResponseModelDto.Fail("No se recibió la configuración a guardar.");
        }

        if (dto.Items.GroupBy(i => i.ItemCodigo, StringComparer.OrdinalIgnoreCase).Any(g => g.Count() > 1))
        {
            return ResponseModelDto.Fail("Hay ítems repetidos en la configuración.");
        }

        if (dto.Items.Any(i => i.Porcentaje < 0m || i.Porcentaje > 100m))
        {
            return ResponseModelDto.Fail("Cada porcentaje debe estar entre 0 y 100.");
        }

        var suma = dto.Items.Sum(i => i.Porcentaje);
        if (suma != 0m && suma != 100m)
        {
            return ResponseModelDto.Fail(
                $"Los porcentajes deben sumar exactamente 100.00 (o todos 0 para desactivar la distribución). Suma actual: {suma:N2}.");
        }

        var validos = (await GetAsync(ct)).Select(i => i.ItemCodigo).ToHashSet(StringComparer.Ordinal);
        var desconocidos = dto.Items.Where(i => !validos.Contains(i.ItemCodigo)).Select(i => i.ItemCodigo).ToList();
        if (desconocidos.Count > 0)
        {
            return ResponseModelDto.Fail($"Ítems no válidos: {string.Join(", ", desconocidos)}.");
        }

        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync(ct);
        }

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        var dbTx = tx.GetDbTransaction();

        await conn.ExecuteAsync(new CommandDefinition(
            "DELETE FROM public.adm_desglose_abono_porcentaje WHERE company_id = @companyId",
            new { companyId }, transaction: dbTx, cancellationToken: ct));

        foreach (var item in dto.Items.Where(i => i.Porcentaje > 0m))
        {
            await conn.ExecuteAsync(new CommandDefinition(@"
                INSERT INTO public.adm_desglose_abono_porcentaje (company_id, item_codigo, porcentaje, usuario)
                VALUES (@companyId, @itemCodigo, @porcentaje, @usuario)",
                new { companyId, itemCodigo = item.ItemCodigo, porcentaje = item.Porcentaje, usuario },
                transaction: dbTx, cancellationToken: ct));
        }

        await tx.CommitAsync(ct);

        return ResponseModelDto.Ok(message: suma == 0m
            ? "Distribución de abonos desactivada."
            : "Porcentajes de distribución guardados.");
    }
}
