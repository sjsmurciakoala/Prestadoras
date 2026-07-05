using Microsoft.AspNetCore.Mvc;
using SIAD.Services.MobileApi;

namespace apc.MobileApi.Controllers;

/// <summary>
/// Medidores de una ruta y snapshot offline V3 (paridad GetRuta /
/// GetOfflineSnapshotV3 del WS). ciclo/anio/mes van en la URI, como el contrato
/// real del WS viejo.
/// </summary>
[ApiController]
[Route("api/rutas")]
public sealed class RutasController : ControllerBase
{
    private readonly ILectoresMobileService _service;

    public RutasController(ILectoresMobileService service)
    {
        _service = service;
    }

    /// <summary>Lista de medidores de la ruta para el ciclo/período.</summary>
    [HttpGet("{ruta}/ciclo/{ciclo:int}/anio/{anio:int}/mes/{mes:int}")]
    public async Task<IActionResult> GetRuta(string ruta, int ciclo, int anio, int mes, CancellationToken ct)
    {
        var medidores = await _service.GetRutaAsync(ruta, ciclo, anio, mes, ct);
        return Ok(medidores);
    }

    /// <summary>Snapshot offline V3 de la ruta (medidores + snapshot por cliente + bloque CAI).</summary>
    [HttpGet("{ruta}/snapshot/ciclo/{ciclo:int}/anio/{anio:int}/mes/{mes:int}")]
    public async Task<IActionResult> GetSnapshot(string ruta, int ciclo, int anio, int mes, CancellationToken ct)
    {
        var snapshot = await _service.GetSnapshotAsync(ruta, ciclo, anio, mes, ct);
        return Ok(snapshot);
    }
}
