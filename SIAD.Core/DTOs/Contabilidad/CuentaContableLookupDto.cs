using System.Text.Json.Serialization;
using SIAD.Core.Utilities;

namespace SIAD.Core.DTOs.Contabilidad;

public sealed class CuentaContableLookupDto
{
    public long AccountId { get; set; }
    public long? BancoCuentaId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? BancoNombre { get; set; }
    public string? NumeroCuenta { get; set; }
    public string? TipoCuenta { get; set; }
    public decimal? SaldoActual { get; set; }
    public string? DisplayText { get; set; }

    [JsonIgnore]
    public string Display => !string.IsNullOrWhiteSpace(DisplayText)
        ? DisplayText
        : string.IsNullOrWhiteSpace(Description)
            ? AccountCodeFormatter.Format(Code)
            : AccountCodeFormatter.FormatDisplay(Code, Description);
}
