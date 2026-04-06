using Microsoft.Extensions.DependencyInjection;

namespace SIAD.Reports;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSiadReports(this IServiceCollection services)
    {
        services.AddScoped<ReportTemplateFactory>();
        services.AddScoped<ReportDraftRegenerationService>();
        services.AddScoped<IInformesCatalogoService, InformesCatalogoService>();
        services.AddScoped<IInformesConsultaService, InformesConsultaService>();
        services.AddScoped<IReportesDatasetService, ReportesDatasetService>();
        services.AddScoped<IReportesDisenoService, ReportesDisenoService>();

        return services;
    }
}
