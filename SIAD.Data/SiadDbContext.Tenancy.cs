using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using SIAD.Core.Tenancy;

namespace SIAD.Data;

public partial class SiadDbContext
{
    private readonly ICurrentCompanyService? _currentCompanyService;

    public SiadDbContext(DbContextOptions<SiadDbContext> options, ICurrentCompanyService currentCompanyService)
        : base(options)
    {
        _currentCompanyService = currentCompanyService;
    }

    private long CurrentCompanyId => _currentCompanyService?.GetCompanyId() ?? 0;

    internal long TenantModelKey => CurrentCompanyId;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
        optionsBuilder.ReplaceService<IModelCacheKeyFactory, SiadDbContextModelCacheKeyFactory>();
        base.OnConfiguring(optionsBuilder);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplyCompanyInformation();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplyCompanyInformation();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void ApplyCompanyScope(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ICompanyScopedEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var method = typeof(SiadDbContext)
                .GetMethod(nameof(SetCompanyQueryFilter), BindingFlags.NonPublic | BindingFlags.Instance)!
                .MakeGenericMethod(entityType.ClrType);
            method.Invoke(this, new object[] { modelBuilder });
        }
    }

    private void SetCompanyQueryFilter<TEntity>(ModelBuilder modelBuilder)
        where TEntity : class, ICompanyScopedEntity
    {
        modelBuilder.Entity<TEntity>().HasQueryFilter(e => e.company_id == CurrentCompanyId);
    }

    private void ApplyCompanyInformation()
    {
        var companyId = CurrentCompanyId;
        if (companyId <= 0)
        {
            return;
        }

        foreach (var entry in ChangeTracker.Entries<ICompanyScopedEntity>())
        {
            if (entry.State == EntityState.Added && entry.Entity.company_id == 0)
            {
                entry.Entity.company_id = companyId;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.company_id).IsModified = false;
            }
        }
    }
}
