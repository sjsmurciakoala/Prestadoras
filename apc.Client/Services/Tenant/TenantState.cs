using System.Threading;
using System.Threading.Tasks;

namespace apc.Client.Services.Tenant;

public sealed class TenantState
{
    private readonly ITenantProvider tenantProvider;
    private readonly TenantSessionClient tenantSessionClient;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private long companyId;

    public TenantState(ITenantProvider tenantProvider, TenantSessionClient tenantSessionClient)
    {
        this.tenantProvider = tenantProvider;
        this.tenantSessionClient = tenantSessionClient;
    }

    public event Action<long>? CompanyChanged;

    public long CompanyId => companyId;

    public bool HasCompany => companyId > 0;

    public async ValueTask<long> EnsureCompanyAsync(CancellationToken cancellationToken = default)
    {
        if (companyId > 0)
        {
            return companyId;
        }

        await initializationLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (companyId == 0)
            {
                companyId = await tenantProvider.GetCompanyIdAsync().ConfigureAwait(false);
            }
        }
        finally
        {
            initializationLock.Release();
        }

        return companyId;
    }

    public async Task<bool> TrySetCompanyIdAsync(long newCompanyId, CancellationToken cancellationToken = default)
    {
        if (newCompanyId <= 0)
        {
            return false;
        }

        if (companyId == newCompanyId)
        {
            return true;
        }

        var updatedCompanyId =
            await tenantSessionClient.SwitchCompanyAsync(newCompanyId, cancellationToken).ConfigureAwait(false);

        if (updatedCompanyId <= 0)
        {
            return false;
        }

        companyId = updatedCompanyId;
        CompanyChanged?.Invoke(companyId);
        return true;
    }
}
