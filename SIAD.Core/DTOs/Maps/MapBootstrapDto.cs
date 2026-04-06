namespace SIAD.Core.DTOs.Maps;

public sealed record MapBootstrapDto(
    string Provider,
    string ApiKey,
    decimal DefaultLatitude,
    decimal DefaultLongitude,
    int DefaultZoom);
