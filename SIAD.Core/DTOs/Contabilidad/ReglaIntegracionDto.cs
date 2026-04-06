namespace SIAD.Core.DTOs.Contabilidad;

public sealed record DocumentTypeLookupDto(
    long DocumentTypeId,
    string Module,
    string Code,
    string Name,
    bool IsActive
);

public sealed record ReglaIntegracionDto(
    long RuleId,
    long CompanyId,
    string Module,
    long DocumentTypeId,
    string DocumentTypeCode,
    string DocumentTypeName,
    string ScenarioCode,
    string? Description,
    long DebitAccountId,
    string DebitAccountCode,
    string DebitAccountName,
    long CreditAccountId,
    string CreditAccountCode,
    string CreditAccountName,
    long? CostCenterId,
    string? CostCenterCode,
    string? CostCenterName,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record ReglaIntegracionUpsertDto(
    string Module,
    long DocumentTypeId,
    string ScenarioCode,
    string? Description,
    long DebitAccountId,
    long CreditAccountId,
    long? CostCenterId,
    bool IsActive,
    string User,
    long? RuleId = null
);

public sealed record ReglaIntegracionFilterDto(
    string? Module,
    long? DocumentTypeId,
    string? Search
);
