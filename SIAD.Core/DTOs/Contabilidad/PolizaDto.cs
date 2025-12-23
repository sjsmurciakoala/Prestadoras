namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// DTO para crear/actualizar pólizas contables
/// </summary>
public sealed record PolizaCrearDto(
    long JournalId,
    long TypeId,
    DateTime VoucherDate,
    string Description,
    string? DocumentRef,
    string? Notes,
    List<PolizaLineaCrearDto> Lineas
);

/// <summary>
/// DTO para líneas de póliza (débito/crédito)
/// </summary>
public sealed record PolizaLineaCrearDto(
    short LineNumber,
    long AccountId,
    long? CostCenterId,
    long? ThirdPartyId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode,
    decimal? ExchangeRate,
    string? Description,
    string? Reference
);

/// <summary>
/// DTO para consultar pólizas (lectura)
/// </summary>
public sealed record PolizaDto(
    long VoucherId,
    long CompanyId,
    long PeriodId,
    long JournalId,
    long TypeId,
    string VoucherNumber,
    DateTime VoucherDate,
    string Description,
    string? DocumentRef,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    string Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PostedAt,
    string? PostedBy
);

/// <summary>
/// DTO para línea de póliza (lectura)
/// </summary>
public sealed record PolizaLineaDto(
    long LineId,
    short LineNumber,
    long AccountId,
    string AccountCode,
    string AccountName,
    long? CostCenterId,
    string? CostCenterCode,
    long? ThirdPartyId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode,
    decimal? ExchangeRate,
    string? Description
);

/// <summary>
/// DTO para apertura de saldos
/// </summary>
public sealed record AperturaSaldoCrearDto(
    long PeriodId,
    long AccountId,
    long? CostCenterId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode,
    decimal? ExchangeRate,
    string? Notes
);

public sealed record AperturaSaldoDto(
    long OpeningId,
    long CompanyId,
    long PeriodId,
    long AccountId,
    string AccountCode,
    string AccountName,
    long? CostCenterId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? CurrencyCode,
    DateTime CreatedAt,
    string CreatedBy
);

/// <summary>
/// DTO para tipo de transacción (catálogo)
/// </summary>
public sealed record TipoTransaccionDto(
    long TypeId,
    long CompanyId,
    string Code,
    string Name,
    string? Description,
    string Category,
    bool IsAutomatic,
    bool AllowsCostCenter,
    bool AllowsThirdParty,
    string Status,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record TipoTransaccionUpsertDto(
    string Code,
    string Name,
    string? Description,
    string Category,
    bool IsAutomatic = false,
    bool AllowsCostCenter = false,
    bool AllowsThirdParty = false
);
