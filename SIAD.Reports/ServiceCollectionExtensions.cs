using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.DTOs.Cobranza;
using SIAD.Reports.Documentos;

namespace SIAD.Reports;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSiadReports(this IServiceCollection services)
    {
        services.AddScoped<IDocumentoCobranzaGenerator, DocumentoCobranzaGenerator>();
        services.AddScoped<ReportTemplateFactory>();
        services.AddScoped<ReportDraftRegenerationService>();
        services.AddScoped<IInformesCatalogoService, InformesCatalogoService>();
        services.AddScoped<IInformesConsultaService, InformesConsultaService>();
        services.AddScoped<IReportesDatasetService, ReportesDatasetService>();
        services.AddScoped<IReportesDisenoService, ReportesDisenoService>();

        return services;
    }
}
