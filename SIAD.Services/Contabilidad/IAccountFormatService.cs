using SIAD.Core.Utilities;

namespace SIAD.Services.Contabilidad;

/// <summary>
/// Máscara y separador de cuentas contables vigentes para la empresa actual
/// (public.con_configuracion_sistema.formato_cuentas / separador_codigo).
/// </summary>
public sealed record AccountFormat(string Mask, string Separator)
{
    public static readonly AccountFormat Default = new(AccountCodeFormatter.DefaultMask, AccountCodeFormatter.DefaultSeparator);

    public string Format(string? code) => AccountCodeFormatter.Format(code, Mask, Separator);

    public string FormatDisplay(string? code, string? description) =>
        AccountCodeFormatter.FormatDisplay(code, description, Mask, Separator);
}

public interface IAccountFormatService
{
    /// <summary>
    /// Devuelve el formato de cuentas configurado para la empresa actual.
    /// Si la empresa no tiene configuración, devuelve <see cref="AccountFormat.Default"/>.
    /// </summary>
    Task<AccountFormat> GetFormatAsync(CancellationToken ct = default);
}
