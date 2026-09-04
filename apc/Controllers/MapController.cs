using apc.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SIAD.Core.DTOs.Maps;
using SIAD.Core.Constants;
using apc.Security;

namespace apc.Controllers;

[ApiController]
[Route("api/map")]
[ModuleAuthorize(PermissionModules.Ventas)]
public sealed class MapController : ControllerBase
{
    private readonly IOptions<MapsOptions> _mapsOptions;

    public MapController(IOptions<MapsOptions> mapsOptions)
    {
        _mapsOptions = mapsOptions;
    }

    [HttpGet("config")]
    public ActionResult<MapBootstrapDto> GetConfig()
    {
        var options = _mapsOptions.Value;
        var zoom = options.DefaultZoom > 0 ? options.DefaultZoom : 13;

        // La clave se resuelve segun el proveedor activo para que el cliente no
        // reciba credenciales de proveedores que no va a usar.
        var apiKey = options.Provider switch
        {
            var p when string.Equals(p, "Google", StringComparison.OrdinalIgnoreCase) => options.GoogleApiKey,
            _ => options.AzureApiKey
        };

        // El Map ID solo tiene sentido en Google (Advanced Markers de DX 25.2).
        var googleMapId = string.Equals(options.Provider, "Google", StringComparison.OrdinalIgnoreCase)
            ? options.GoogleMapId
            : string.Empty;

        return Ok(new MapBootstrapDto(
            options.Provider,
            apiKey,
            options.DefaultLatitude,
            options.DefaultLongitude,
            zoom,
            googleMapId));
    }
}
