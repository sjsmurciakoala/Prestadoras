using System.Text.Json.Serialization;

namespace SIAD.Core.DTOs.Contabilidad;

public sealed class CuentaContableLookupDto
{
    public long AccountId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    [JsonIgnore]
    public string Display => string.IsNullOrWhiteSpace(Description)
        ? Code
        : $"{Code} - {Description}";
}
