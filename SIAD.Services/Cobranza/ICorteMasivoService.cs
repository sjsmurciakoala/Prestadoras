using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SIAD.Core.DTOs.Cobranza;

namespace SIAD.Services.Cobranza;

public interface ICorteMasivoService
{
    Task<CorteMasivoHdrDto> GenerarAsync(GenerarCorteMasivoRequest request, string usuario, CancellationToken ct = default);
    Task<IReadOnlyList<CorteMasivoHdrDto>> ListarAsync(CancellationToken ct = default);
    Task<CorteMasivoDetalleDto?> ObtenerDetalleAsync(int hdrId, CancellationToken ct = default);
    Task<CorteMasivoDetalleDto?> ObtenerParaReimpresionAsync(int hdrId, CancellationToken ct = default);

    /// <summary>
    /// Cancela las órdenes de trabajo de corte (tipo 33) que sigan pendientes para el
    /// cliente y marca como pagados sus detalles de corte vigentes (los excluye de la
    /// reimpresión). Pensado para invocarse cuando un pago deja al cliente sin saldo.
    /// Participa en la transacción que el llamador tenga abierta sobre el mismo DbContext.
    /// Devuelve la cantidad de órdenes canceladas.
    /// </summary>
    Task<int> CancelarOrdenesCorteClienteAsync(string clienteClave, string usuario, CancellationToken ct = default);
}
