namespace apc.Options;

public sealed class MapsOptions
{
    public const string SectionName = "Maps";

    public string Provider { get; set; } = "Azure";
    public string AzureApiKey { get; set; } = string.Empty;
    public string GoogleApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Map ID de Google (Google Cloud → Administración de mapas). DevExpress
    /// 25.2 dibuja los marcadores con Advanced Markers, que EXIGEN un Map ID;
    /// sin él sale "El mapa se ha inicializado sin un ID de mapa válido" y no
    /// se pintan los pines. Solo aplica al proveedor Google.
    /// </summary>
    public string GoogleMapId { get; set; } = string.Empty;

    public decimal DefaultLatitude { get; set; } = 14.0723m;
    public decimal DefaultLongitude { get; set; } = -87.1921m;
    public int DefaultZoom { get; set; } = 13;
}
