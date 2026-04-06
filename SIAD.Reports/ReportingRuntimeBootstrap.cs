namespace SIAD.Reports;

public static class ReportingRuntimeBootstrap
{
    public static void Initialize(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
    }
}
