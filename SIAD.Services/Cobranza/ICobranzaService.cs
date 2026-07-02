using SIAD.Core.DTOs.Cobranza;
using SIAD.Core.DTOs.Common;

namespace SIAD.Services.Cobranza;

public interface ICobranzaService
{
    Task<IReadOnlyList<CobranzaSaldoDetalleDto>> ObtenerSaldosClienteAsync(string clienteClave, CancellationToken ct = default);
    Task<bool> EstaBloqueadoAsync(string clienteClave, CancellationToken ct = default);
    Task<string?> NumeroALetrasAsync(decimal numero, CancellationToken ct = default);
    Task<CobranzaPlanPreviewDto> CalcularCuotasAsync(CobranzaPlanPreviewRequestDto dto, CancellationToken ct = default);
    Task<ResponseModelDto> GuardarPlanPagoAsync(CobranzaPlanGuardarDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<CobranzaPlanResumenDto>> ListarPlanesAsync(CancellationToken ct = default);
    Task<CobranzaPlanDetalleDto?> ObtenerPlanAsync(string correlativo, CancellationToken ct = default);
    // Acciones de cobranza
    Task<IReadOnlyList<AccionCobranzaDto>> ListarAccionesAsync(string clienteClave, CancellationToken ct = default);
    Task<IReadOnlyList<AccionCobranzaHistorialDto>> ListarHistorialAccionesAsync(
        DateTime desde, DateTime hasta, int? codAccion, string? clienteClave,
        string? ejecutadoPor, CancellationToken ct = default);
    Task<IReadOnlyList<AccionCobranzaCatalogoDto>> ObtenerCatalogoAccionesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ObservacionCobranzaCatalogoDto>> ObtenerCatalogoObservacionesAsync(CancellationToken ct = default);
    Task<IReadOnlyList<AbogadoCobranzaLookupDto>> ObtenerAbogadosAsync(CancellationToken ct = default);
    Task<RegistrarAccionResultadoDto> RegistrarAccionAsync(RegistrarAccionCobranzaRequest request, string ejecutadoPor, CancellationToken ct = default);
    Task<int?> RegenerarDocumentoAccionAsync(int accionId, string usuario, CancellationToken ct = default);
    Task<DocumentoGenerado?> ObtenerDocumentoAccionAsync(int documentoId, CancellationToken ct = default);

    // Bloqueo
    Task<BloqueoClienteEstadoDto?> ObtenerEstadoBloqueoAsync(string clienteClave, CancellationToken ct = default);
    Task BloquearDesbloquearAsync(string clienteClave, bool bloquear, string? motivo, string usuario, CancellationToken ct = default);
    Task<IReadOnlyList<LlamadaCobranzaDto>> ListarLlamadasAsync(string clienteClave, CancellationToken ct = default);
    Task RegistrarLlamadaAsync(RegistrarLlamadaRequest request, string usuario, CancellationToken ct = default);
    Task<IReadOnlyList<NotaCobroDto>> ListarNotasCobroAsync(string clienteClave, CancellationToken ct = default);
    Task<NotaCobroDto> EmitirNotaCobroAsync(EmitirNotaCobroRequest request, string usuario, CancellationToken ct = default);
    Task AnularNotaCobroAsync(int id, string motivo, CancellationToken ct = default);

    // CRUD Catálogos
    Task<IReadOnlyList<AccionCobranzaCrudDto>> ListarAccionesCrudAsync(CancellationToken ct = default);
    Task GuardarAccionAsync(AccionCobranzaSaveDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<ObservacionCobranzaCrudDto>> ListarObservacionesCrudAsync(CancellationToken ct = default);
    Task GuardarObservacionAsync(ObservacionCobranzaSaveDto dto, CancellationToken ct = default);

    // Clientes para cobros (acciones en lote / cartas de cobro)
    Task<IReadOnlyList<ClienteCobroDto>> ListarClientesCobroAsync(ClienteCobroFiltroDto filtro, CancellationToken ct = default);

    // Cartera vencida (antigüedad por tramos)
    Task<IReadOnlyList<CarteraVencidaClienteDto>> ListarCarteraVencidaAsync(
        CarteraVencidaFiltroDto filtro, CancellationToken ct = default);
    Task<int> RegistrarAccionLoteAsync(RegistrarAccionLoteRequest request, string ejecutadoPor, CancellationToken ct = default);
    Task<CartaCobroHdrDto> GenerarCartasCobroAsync(GenerarCartasCobroRequest request, string usuario, CancellationToken ct = default);
    Task<CartaCobroLoteDto?> ObtenerCartaLoteAsync(int hdrId, CancellationToken ct = default);
}
