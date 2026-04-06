namespace SIAD.Core.DTOs.Tenant;

public sealed class TenantCompanyContextDto
{
    public long CurrentCompanyId { get; set; }

    public bool HasValidCompany { get; set; }

    public bool HasCompanies { get; set; }

    public bool CanManageCompanies { get; set; }

    public string? Message { get; set; }

    public string? RecoveryPath { get; set; }
}
