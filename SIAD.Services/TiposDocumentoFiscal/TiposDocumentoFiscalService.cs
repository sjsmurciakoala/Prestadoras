using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.TiposDocumentoFiscal;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.TiposDocumentoFiscal;

public sealed class TiposDocumentoFiscalService : ITiposDocumentoFiscalService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public TiposDocumentoFiscalService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<TipoDocumentoFiscalDto>> ListarAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var conn = _context.Database.GetDbConnection();

        // count de CAIs por tipo dentro de la company actual (informativo)
        const string sql = @"
            SELECT
                t.tipo_documento_fiscal_id      AS ""TipoDocumentoFiscalId"",
                t.codigo                        AS ""Codigo"",
                t.descripcion                   AS ""Descripcion"",
                t.es_comprobante_fiscal         AS ""EsComprobanteFiscal"",
                t.es_documento_complementario   AS ""EsDocumentoComplementario"",
                t.requiere_factura_origen       AS ""RequiereFacturaOrigen"",
                t.activo                        AS ""Activo"",
                COALESCE(c.cnt, 0)::int         AS ""CaisAsociados""
            FROM cfg_tipo_documento_fiscal t
            LEFT JOIN (
                SELECT tipo_documento_fiscal_id, COUNT(*) AS cnt
                FROM adm_cai_facturacion
                WHERE company_id = @companyId
                GROUP BY tipo_documento_fiscal_id
            ) c ON c.tipo_documento_fiscal_id = t.tipo_documento_fiscal_id
            ORDER BY t.tipo_documento_fiscal_id;";

        var rows = await conn.QueryAsync<TipoDocumentoFiscalDto>(
            new CommandDefinition(sql, new { companyId }, cancellationToken: ct));
        return rows.ToList();
    }

    public async Task<ResponseModelDto> ActualizarAsync(short id, TipoDocumentoFiscalUpdateDto dto, string usuario, CancellationToken ct = default)
    {
        if (dto is null) return ResponseModelDto.Fail("Datos invalidos.");
        if (string.IsNullOrWhiteSpace(dto.Descripcion)) return ResponseModelDto.Fail("La descripcion es obligatoria.");

        var conn = _context.Database.GetDbConnection();

        const string sql = @"
            UPDATE cfg_tipo_documento_fiscal
            SET descripcion                  = @descripcion,
                es_comprobante_fiscal        = @esComprobanteFiscal,
                es_documento_complementario  = @esDocumentoComplementario,
                requiere_factura_origen      = @requiereFacturaOrigen,
                activo                       = @activo
            WHERE tipo_documento_fiscal_id = @id;";

        var rows = await conn.ExecuteAsync(new CommandDefinition(sql, new
        {
            id,
            descripcion = dto.Descripcion.Trim(),
            esComprobanteFiscal = dto.EsComprobanteFiscal,
            esDocumentoComplementario = dto.EsDocumentoComplementario,
            requiereFacturaOrigen = dto.RequiereFacturaOrigen,
            activo = dto.Activo
        }, cancellationToken: ct));

        return rows > 0
            ? ResponseModelDto.Ok(id, "Tipo de documento actualizado.")
            : ResponseModelDto.Fail("Tipo de documento no encontrado.");
    }
}
