using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.Constants;
using SIAD.Data;

namespace SIAD.Reports;

public sealed class ReportDraftRegenerationService
{
    private readonly SiadDbContext _context;
    private readonly ReportTemplateFactory _templateFactory;

    public ReportDraftRegenerationService(
        SiadDbContext context,
        ReportTemplateFactory templateFactory)
    {
        _context = context;
        _templateFactory = templateFactory;
    }

    public async Task EnsureDraftLayoutAsync(long companyId, string codigo, string actor, CancellationToken ct = default)
        => await UpsertDraftLayoutAsync(
            companyId,
            codigo,
            actor,
            requireAssignedDataset: false,
            overwriteExistingDraft: false,
            ct);

    public async Task RegenerarBorradorDesdeDatasetActualAsync(long companyId, string codigo, string actor, CancellationToken ct = default)
        => await UpsertDraftLayoutAsync(
            companyId,
            codigo,
            actor,
            requireAssignedDataset: true,
            overwriteExistingDraft: true,
            ct);

    private async Task UpsertDraftLayoutAsync(
        long companyId,
        string codigo,
        string actor,
        bool requireAssignedDataset,
        bool overwriteExistingDraft,
        CancellationToken ct)
    {
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No fue posible determinar la empresa actual.");
        }

        var normalizedCode = ReportesWebConstants.NormalizeCode(codigo);
        if (!ReportesWebConstants.IsValidCode(normalizedCode))
        {
            throw new ArgumentException("El código del reporte solo admite letras, números, guion y guion bajo.");
        }

        try
        {
            var catalogo = await _context.rep_catalogo_informes
                .FirstOrDefaultAsync(
                    x => x.company_id == companyId
                         && x.tipo_origen == ReportesWebConstants.TipoOrigenReporte
                         && x.codigo == normalizedCode
                         && x.is_active,
                    ct);

            if (catalogo is null)
            {
                throw new InvalidOperationException("El reporte solicitado no está registrado.");
            }

            if (requireAssignedDataset && string.IsNullOrWhiteSpace(catalogo.consulta_clave))
            {
                throw new InvalidOperationException("El reporte no tiene un dataset asignado. No es posible regenerar el borrador.");
            }

            if (requireAssignedDataset)
            {
                var dataset = await _context.rep_catalogo_datasets
                    .AsNoTracking()
                    .FirstOrDefaultAsync(
                        x => x.company_id == companyId
                             && x.codigo == catalogo.consulta_clave
                             && x.is_active,
                        ct);

                if (dataset is null)
                {
                    throw new InvalidOperationException("El dataset asignado no existe o está inactivo. Corrija la asignación antes de regenerar el borrador.");
                }
            }

            var existingDraft = await _context.rep_reporte_layouts
                .FirstOrDefaultAsync(
                    x => x.company_id == companyId
                         && x.informe_id == catalogo.informe_id
                         && x.estado == ReportesWebConstants.LayoutStatus.Draft,
                    ct);

            if (existingDraft is not null && !overwriteExistingDraft)
            {
                return;
            }

            var now = DateTime.UtcNow;
            var layoutXml = Encoding.UTF8.GetString(
                _templateFactory.CreateLayoutBytes(
                    catalogo.codigo,
                    catalogo.nombre,
                    catalogo.descripcion,
                    catalogo.consulta_clave));

            await using var transaction = await _context.Database.BeginTransactionAsync(ct);

            var draft = existingDraft;

            if (draft is null)
            {
                var nextVersion = (await _context.rep_reporte_layouts
                    .Where(x => x.company_id == companyId && x.informe_id == catalogo.informe_id)
                    .Select(x => (int?)x.version_num)
                    .MaxAsync(ct) ?? 0) + 1;

                draft = new SIAD.Core.Entities.rep_reporte_layout
                {
                    company_id = companyId,
                    informe_id = catalogo.informe_id,
                    version_num = nextVersion,
                    estado = ReportesWebConstants.LayoutStatus.Draft,
                    layout_xml = layoutXml,
                    created_at = now,
                    created_by = actor
                };

                _context.rep_reporte_layouts.Add(draft);
            }
            else
            {
                draft.layout_xml = layoutXml;
                draft.updated_at = now;
                draft.updated_by = actor;
            }

            catalogo.updated_at = now;
            catalogo.updated_by = actor;

            await _context.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (Exception ex) when (IsTableMissing(ex))
        {
            throw new InvalidOperationException(
                "La estructura de reportería no está completa. Ejecute los scripts 2026-03-21_add_rep_reporte_layout.sql y 2026-03-21_add_rep_catalogo_dataset.sql.",
                ex);
        }
    }

    private static bool IsTableMissing(Exception ex)
    {
        if (ex is PostgresException postgresException &&
            postgresException.SqlState == PostgresErrorCodes.UndefinedTable)
        {
            return true;
        }

        return ex.InnerException is not null && IsTableMissing(ex.InnerException);
    }
}
