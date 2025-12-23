using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Servicio para gestión de pólizas contables (encabezado + líneas)
/// Alineado con estructura DB: con_poliza (header) + con_poliza_linea (detail)
/// Multiempresa: todas las operaciones scoped por company_id
/// </summary>
public interface IPolizaService
{
    /// <summary>Crear nueva póliza en estado DRAFT</summary>
    Task<long> CrearAsync(
        long companyId,
        long? periodId,
        long? journalId,
        DateTime polizaDate,
        string module,
        string documentType,
        string description,
        List<PolizaLineaCrearDto> lineas,
        string userId,
        CancellationToken ct = default
    );

    /// <summary>Obtener póliza con todas sus líneas</summary>
    Task<PolizaConLineasDto> ObtenerAsync(long companyId, long polizaId, CancellationToken ct = default);

    /// <summary>Listar pólizas por período</summary>
    Task<List<PolizaListaDto>> ListarPorPeriodoAsync(long companyId, long periodId, int skip = 0, int take = 100, CancellationToken ct = default);

    /// <summary>Listar pólizas por diario</summary>
    Task<List<PolizaListaDto>> ListarPorDiarioAsync(long companyId, long journalId, int skip = 0, int take = 100, CancellationToken ct = default);

    /// <summary>Actualizar póliza (solo si está en DRAFT)</summary>
    Task ActualizarAsync(long companyId, long polizaId, PolizaActualizarDto dto, string userId, CancellationToken ct = default);

    /// <summary>Agregar línea a póliza (solo si está en DRAFT)</summary>
    Task AgregarLineaAsync(long companyId, long polizaId, PolizaLineaCrearDto linea, string userId, CancellationToken ct = default);

    /// <summary>Eliminar línea de póliza (solo si está en DRAFT)</summary>
    Task EliminarLineaAsync(long companyId, long lineaId, CancellationToken ct = default);

    /// <summary>Eliminar póliza completa (solo si está en DRAFT)</summary>
    Task EliminarAsync(long companyId, long polizaId, CancellationToken ct = default);

    /// <summary>Verificar que débitos = créditos</summary>
    Task<(bool balanceado, decimal debitTotal, decimal creditTotal)> ValidarBalanceAsync(long companyId, long polizaId, CancellationToken ct = default);

    /// <summary>Registrar póliza (cambiar estado a POSTED y actualizar saldos)</summary>
    Task RegistrarAsync(long companyId, long polizaId, string userId, CancellationToken ct = default);

    /// <summary>Revertir póliza registrada (POSTED → DRAFT y revertir saldos)</summary>
    Task RevertirAsync(long companyId, long polizaId, string userId, CancellationToken ct = default);
}

/// <summary>Datos para crear póliza</summary>
public sealed record PolizaCrearDto(
    long? PeriodId,
    long? JournalId,
    DateTime PolizaDate,
    string Module,
    string DocumentType,
    string? Description,
    List<PolizaLineaCrearDto> Lineas
);

/// <summary>Datos para crear línea de póliza</summary>
public sealed record PolizaLineaCrearDto(
    long AccountId,
    long? CostCenterId,
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    string? CurrencyCode = "HNL",
    decimal? ExchangeRate = null,
    string? SourceDocument = null
);

/// <summary>Datos para actualizar póliza</summary>
public sealed record PolizaActualizarDto(
    DateTime PolizaDate,
    string? Description
);

/// <summary>DTO para consultar póliza con todas sus líneas</summary>
public sealed record PolizaConLineasDto(
    long PolizaId,
    long CompanyId,
    long? PeriodId,
    long? JournalId,
    string PolizaNumber,
    DateTime PolizaDate,
    string Module,
    string DocumentType,
    string? Description,
    string Status,
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
    decimal DebitAmount,
    decimal CreditAmount,
    string? Description,
    string? CurrencyCode
);

/// <summary>DTO para listar pólizas (vista simple)</summary>
public sealed record PolizaListaDto(
    long PolizaId,
    string PolizaNumber,
    DateTime PolizaDate,
    string Module,
    string DocumentType,
    string? Description,
    string Status,
    decimal TotalDebit,
    decimal TotalCredit,
    bool IsBalanced,
    DateTime CreatedAt,
    string CreatedBy
);
