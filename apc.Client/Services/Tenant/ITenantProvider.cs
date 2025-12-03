using System.Threading.Tasks;

namespace apc.Client.Services.Tenant;

public interface ITenantProvider
{
    ValueTask<long> GetCompanyIdAsync();
}
