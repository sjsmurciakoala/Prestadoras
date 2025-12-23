namespace SIAD.Core.DTOs.Contabilidad;

public record PlanCuentaDto(
    long AccountId,
    long? ParentAccountId,
    string Code,
    string Name,
    string AccountType,
    string? Category,
    short Level,
    bool AllowsPosting,
    bool AllowsBudget,
    bool AllowsThird,
    bool IsTaxBase,
    bool AllowsCostCenter,
    bool AllowsMultiCurrency,
    long? AdjustmentAccountId,
    long? CorrectionAccountId,
    string Status,
    string? Description,
    string? CurrencyCode);
