namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>DTO para consultar póliza con todas sus líneas</summary>
public sealed record PolizaConLineasDto(
    long PolizaId,
    long CompanyId,
    long? PeriodId,
    long? JournalId,
    long? TypeId,
    string PolizaNumber,
    DateTime PolizaDate,
    string Module,
    string DocumentType,
    string? DocumentNumber,
    string? Description,
    short Status,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    List<PolizaLineaConDetallesDto> Lineas
);

/// <summary>DTO para línea de póliza con detalles de cuenta</summary>
public sealed record PolizaLineaConDetallesDto(
    long LineaId,
    long PolizaId,
    long AccountId,
    string AccountCode,
    string AccountName,
    long? CostCenterId,
    string? CostCenterCode,
    long? ThirdPartyId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    string? CurrencyCode
);

/// <summary>DTO para listar pólizas (vista simple)</summary>
public sealed record PolizaListaDto(
    long PolizaId,
    long? TypeId,
    string PolizaNumber,
    DateTime PolizaDate,
    string Module,
    string DocumentType,
    string? DocumentNumber,
    string? Description,
    short Status,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime CreatedAt,
    string CreatedBy
);
