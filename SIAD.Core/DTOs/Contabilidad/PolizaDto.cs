namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// DTO para crear/actualizar polizas contables
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
/// DTO para lineas de poliza (debito/credito)
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
/// DTO para consultar polizas (lectura)
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
    short Status,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? PostedAt,
    string? PostedBy
);

/// <summary>
/// DTO para linea de poliza (lectura)
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
/// DTO para tipo de transaccion (catalogo)
/// </summary>
public sealed record TipoTransaccionDto(
    long TypeId,
    long companyId,
    string Code,
    string Name,
    String? Desciption,
    string Category,
    short TypeTrans,
    short TypeOper,
    short Frequency,
    int MaxEntries,
    bool AllowsCostCenter,
    bool AllowsThirdParty,
    bool AllowsCashFlow,
    bool AllowsAccountLimit,
    bool IsDefault,
    bool IsAutomatic,
    string Status,
    DateTime CreatedAt,
    string CreatedBy,
    short? StatusId = null,
    long DocumentSequenceStart = 1,
    long LastDocumentNumber = 0,
    long NextDocumentNumber = 1,
    bool HasPolizas = false
);

public sealed record TipoTransaccionUpsertDto(
    string Code,
    string Name,
    string? Description,
    string Category,
    short TypeTrans,
    short TypeOper,
    short Frequency,
    int MaxEntries,
    bool AllowsCostCenter,
    bool AllowsThirdParty,
    bool AllowsCashFlow,
    bool AllowsAccountLimit,
    bool IsDefault,
    bool IsAutomatic,
    string User,
    long? TypeId = null,
    short? StatusId = null,
    string? Status = null,
    long DocumentSequenceStart = 1
);

public sealed record TipoTransaccionRuleDto(
    long RuleId,
    long CompanyId,
    long TypeId,
    int LineNumber,
    string? AccountCodeFrom,
    string? AccountCodeTo,
    string? CostCenterCodeFrom,
    string? CostCenterCodeTo,
    string? ThirdPartyCodeFrom,
    string? ThirdPartyCodeTo,
    bool IsActive,
    DateTime CreatedAt,
    string CreatedBy
);

public sealed record TipoTransaccionRuleUpsertDto(
    long TypeId,
    int LineNumber,
    string? AccountCodeFrom,
    string? AccountCodeTo,
    string? CostCenterCodeFrom,
    string? CostCenterCodeTo,
    string? ThirdPartyCodeFrom,
    string? ThirdPartyCodeTo,
    bool IsActive,
    string User,
    long? RuleId = null
);
