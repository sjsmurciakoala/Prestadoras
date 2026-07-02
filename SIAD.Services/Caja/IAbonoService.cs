using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Caja;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.CaptacionPagos;

namespace SIAD.Services.Caja;

public interface IAbonoService
{
    Task<IReadOnlyList<FacturaConSaldoDto>> BuscarFacturasConSaldoAsync(string term, CancellationToken ct = default);
    Task<IReadOnlyList<FacturaConSaldoDto>> ListarFacturasPendientesPorClienteAsync(string clienteClave, CancellationToken ct = default);
    Task<ResponseModelDto> RegistrarAbonoAsync(AbonoCrearDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> ReversarAbonoAsync(ReversoAbonoRequestDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ArqueoDto>> ListarAbonosDelDiaAsync(string? usuario, DateTime? fecha, CancellationToken ct = default);
    Task<ReciboAbonoDto?> GenerarDatosReciboAsync(int transaccionId, CancellationToken ct = default);
    Task<IReadOnlyList<AbonoHistorialItemDto>> ListarHistorialPorClienteAsync(string clienteClave, CancellationToken ct = default);

    // Paso 1 — generar recibo pendiente desde Maestro Clientes
    Task<ResponseModelDto> GenerarReciboPendienteAsync(GenerarReciboDto dto, CancellationToken ct = default);

    // Caja — consultar recibos pendientes de una factura específica
    Task<IReadOnlyList<ReciboPendienteDto>> ListarRecibosPendientesPorFacturaAsync(string numFactura, CancellationToken ct = default);

    // Caja — historial de abonos ya aplicados de una factura específica
    Task<IReadOnlyList<AbonoHistorialItemDto>> ListarAbonosPorFacturaAsync(string numFactura, CancellationToken ct = default);

    // Caja — anular un recibo pendiente (sin haber procesado el pago)
    Task<ResponseModelDto> AnularReciboPendienteAsync(AnularReciboPendienteDto dto, CancellationToken ct = default);
}
