namespace SIAD.Core.DTOs.Contabilidad;

public record PlanCuentaUpsertDto(
    string Code,
    string Name,
    string AccountType,
    bool AllowsPosting,
    bool AllowsBudget,
    bool AllowsThird,
    bool IsTaxBase,
    bool AllowsCostCenter,
    bool AllowsMultiCurrency,
    string Status,
    string User,
    long? AccountId = null,
    long? ParentAccountId = null,
    string? Category = null,
    string? Description = null,
    string? CurrencyCode = null,
    long? AdjustmentAccountId = null,
    long? CorrectionAccountId = null);
