using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace SIAD.Data;

internal sealed class SiadDbContextModelCacheKeyFactory : IModelCacheKeyFactory
{
    public object Create(DbContext context, bool designTime)
    {
        if (context is SiadDbContext siadContext)
        {
            return (context.GetType(), siadContext.TenantModelKey, designTime);
        }

        return (context.GetType(), designTime);
    }
}
