namespace SIAD.Core.DTOs.Contabilidad;

public record PlanCuentaUpsertDto(
    string Code,
    string Name,
    string AccountType,
    bool AllowsPosting,
    string Status,
    string User,
    long? AccountId = null,
    long? ParentAccountId = null,
    string? Category = null,
    string? Description = null,
    string? CurrencyCode = null,
    string? ShortDescription = null,
    string? ExternalReference = null,
    bool AllowsBudget = false,
    bool AllowsCostCenter = false,
    bool AllowsThird = false,
    bool AllowsBank = false,
    bool IsTaxBase = false,
    bool AllowsAmount = true,
    bool AllowsMultiCurrency = false,
    decimal? BudgetAmount = null);
