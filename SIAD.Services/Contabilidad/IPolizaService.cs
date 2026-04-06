using SIAD.Core.DTOs.Contabilidad;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Servicio para gestión de pólizas contables (encabezado + líneas)
/// Alineado con estructura DB: con_partida_hdr (header) + con_partida_dtl (detail)
/// Multiempresa: todas las operaciones scoped por company_id
/// </summary>
public interface IPolizaService
{
    /// <summary>Crear nueva póliza en estado DRAFT</summary>
    Task<long> CrearAsync(
        long companyId,
        long typeId,
        long? periodId,
        long? journalId,
        DateTime polizaDate,
        string module,
        string documentType,
        long? documentId,
        string? documentNumber,
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


