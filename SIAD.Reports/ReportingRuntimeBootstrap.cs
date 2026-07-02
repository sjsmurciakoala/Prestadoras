using DevExpress.DataAccess.Sql;
using DevExpress.XtraReports;
using DevExpress.XtraReports.UI;
using Microsoft.Extensions.Configuration;

namespace SIAD.Reports;

public static class ReportingRuntimeBootstrap
{
    public static void Initialize(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }

    public static void ConfigureSqlDataSources(XtraReport report, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(configuration);

        foreach (var dataSource in DataSourceManager.GetDataSources<SqlDataSource>(report, includeSubReports: true))
        {
            var connectionName = string.IsNullOrWhiteSpace(dataSource.ConnectionName)
                ? SIAD.Core.Constants.ReportesWebConstants.DefaultReportingConnectionName
                : dataSource.ConnectionName;

            dataSource.ConnectionParameters =
                ReportingPostgreSqlConnectionResolver.Resolve(configuration, connectionName);
        }
    }
}
