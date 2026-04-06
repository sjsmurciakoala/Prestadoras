using apc.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using SIAD.Core.DTOs.Maps;

namespace apc.Controllers;

[ApiController]
[Route("api/map")]
[Authorize]
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

        return Ok(new MapBootstrapDto(
            options.Provider,
            options.AzureApiKey,
            options.DefaultLatitude,
            options.DefaultLongitude,
            zoom));
    }
}
