namespace apc.Options;

public sealed class MapsOptions
{
    public const string SectionName = "Maps";

    public string Provider { get; set; } = "Azure";
    public string AzureApiKey { get; set; } = string.Empty;
    public decimal DefaultLatitude { get; set; } = 14.0723m;
    public decimal DefaultLongitude { get; set; } = -87.1921m;
    public int DefaultZoom { get; set; } = 13;
}
