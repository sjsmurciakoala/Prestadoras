namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>Request para crear póliza</summary>
public sealed record PolizaCrearRequest(
    long TypeId,
    long? PeriodId,
    long? JournalId,
    DateTime PolizaDate,
    string Module,
    string DocumentType,
    long? DocumentId,
    string? DocumentNumber,
    string? Description,
    List<PolizaLineaRequest> Lineas
);

/// <summary>Request para crear línea de póliza</summary>
public sealed record PolizaLineaRequest(
    long AccountId,
    long? CostCenterId,
    long? ThirdPartyId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    string? CurrencyCode,
    decimal? ExchangeRate,
    string? SourceDocument
);

/// <summary>Request para actualizar póliza</summary>
public sealed record PolizaActualizarRequest(
    DateTime PolizaDate,
    string? Description
);
