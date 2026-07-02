using System;

namespace SIAD.Core.DTOs.Contabilidad;

public record ReglaIntegracionListDto(
    long ReglaId,
    long CompanyId,
    string Module,
    long DocumentTypeId,
    string DocumentTypeCode,
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
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy
);

public record ReglaIntegracionUpsertDto(
    long? ReglaId,
    string ScenarioCode,
    string? Description,
    long DebitAccountId,
    long CreditAccountId,
    long? CostCenterId,
    bool IsActive,
    string User
);
