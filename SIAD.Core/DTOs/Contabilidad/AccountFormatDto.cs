using SIAD.Core.Utilities;

namespace SIAD.Core.DTOs.Contabilidad;

/// <summary>
/// Máscara de cuentas contables de la empresa actual
/// (con_configuracion_sistema.formato_cuentas / separador_codigo).
/// </summary>
public sealed class AccountFormatDto
{
    public string FormatoCuentas { get; set; } = AccountCodeFormatter.DefaultMask;

    public string SeparadorCodigo { get; set; } = AccountCodeFormatter.DefaultSeparator;
}
