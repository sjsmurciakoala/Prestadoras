using DevExpress.DataAccess.Sql;
using DevExpress.XtraReports;
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.Configuration;

namespace SIAD.Reports;

public static class ReportingRuntimeBootstrap
{
    // Npgsql corta cualquier comando a los 30 s si nadie fija otra cosa. Varios
    // informes de cobranza recorren el historial completo de movimientos y pasan
    // de ese margen: el 2026-09-04 el log de produccion registro cuatro cancelaciones
    // de "Saldos de Clientes por Categoria", que el portal mostraba como un 500 seco.
    // Se ajusta con la clave Reportes:DbCommandTimeoutSegundos; 0 seria sin limite,
    // que no queremos: preferimos que un informe desbocado falle a que cuelgue el pool.
    private const int DbCommandTimeoutPorDefecto = 180;

    public static void Initialize(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public static void ConfigureSqlDataSources(XtraReport report, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(configuration);

        var commandTimeout = ResolverDbCommandTimeout(configuration);

        foreach (var dataSource in DataSourceManager.GetDataSources<SqlDataSource>(report, includeSubReports: true))
        {
            var connectionName = string.IsNullOrWhiteSpace(dataSource.ConnectionName)
                ? SIAD.Core.Constants.ReportesWebConstants.DefaultReportingConnectionName
                : dataSource.ConnectionName;

            dataSource.ConnectionParameters =
                ReportingPostgreSqlConnectionResolver.Resolve(configuration, connectionName);

            if (dataSource.ConnectionOptions is not null)
            {
                dataSource.ConnectionOptions.DbCommandTimeout = commandTimeout;
            }
        }
    }

    private static int ResolverDbCommandTimeout(IConfiguration configuration)
    {
        var configurado = configuration.GetValue<int?>("Reportes:DbCommandTimeoutSegundos");

        return configurado is > 0 ? configurado.Value : DbCommandTimeoutPorDefecto;
    }
}
