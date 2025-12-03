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
    string Status,
    string? Description,
    string? CurrencyCode);
