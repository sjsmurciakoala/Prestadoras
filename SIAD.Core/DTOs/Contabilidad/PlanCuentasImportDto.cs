namespace SIAD.Core.DTOs.Contabilidad;
public record PlanCuentasImportRow(

    string Code,
    string Name, 
    string AccountType,
    string? ParentCode,
    string? Category,
    bool? AllowsPosting,
    string? Status,
    string? CurrencyCode,
    string? Description);
public record PlanCuentasImportError(int RowNumber, string Code, string Message);
public record PlanCuentasImportResult(
    int Inserted,
    int Updated, 
    IReadOnlyList<PlanCuentasImportError> Errors);

