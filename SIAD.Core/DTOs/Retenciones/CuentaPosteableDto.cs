namespace SIAD.Core.DTOs.Retenciones;

/// <summary>
/// Cuenta contable posteable del plan de la empresa actual, para el desplegable de selección de la
/// cuenta del pasivo. Solo cuentas con <c>allows_posting = true</c>.
/// </summary>
public sealed class CuentaPosteableDto
{
    public long AccountId { get; init; }
    public string Codigo { get; init; } = string.Empty;
    public string Nombre { get; init; } = string.Empty;

    public string Display => $"{Codigo} - {Nombre}";
}
