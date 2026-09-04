using System.Text;
using System.Text.RegularExpressions;

namespace SIAD.Core.Utilities;

/// <summary>
/// Formato configurable de los códigos fiscales que se transcriben del proveedor
/// (No. de factura SAR, CAI). Es el equivalente de <see cref="AccountCodeFormatter"/>
/// para el catálogo <c>cfg_formato_fiscal</c>.
/// </summary>
/// <remarks>
/// Notación de la máscara, la que teclea el usuario en el mantenimiento:
/// <list type="bullet">
///   <item><description><c>#</c> — dígito obligatorio (0-9).</description></item>
///   <item><description><c>X</c> — letra o dígito obligatorio (A-Z, 0-9).</description></item>
///   <item><description><c>H</c> — hexadecimal obligatorio (0-9, A-F).</description></item>
///   <item><description>cualquier otro carácter es un literal (guion, punto, barra...).</description></item>
/// </list>
/// Todos los métodos son tolerantes: nunca lanzan, ni con la máscara vacía ni con valores
/// más cortos o más largos de lo que la máscara admite. Quien valida es
/// <see cref="EsValido"/>, no el formateo.
/// </remarks>
public static class FiscalCodeFormatter
{
    /// <summary>Metacarácter de dígito.</summary>
    public const char MetaDigito = '#';

    /// <summary>Metacarácter alfanumérico.</summary>
    public const char MetaAlfanumerico = 'X';

    /// <summary>Metacarácter hexadecimal.</summary>
    public const char MetaHexadecimal = 'H';

    /// <summary>Caracteres que DevExpress interpreta en una máscara de texto y hay que escapar.</summary>
    private const string DevExpressReservados = "LlAaCc09#><\\/:$";

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);

    public static bool EsMetacaracter(char ch) => ch is MetaDigito or MetaAlfanumerico or MetaHexadecimal;

    /// <summary>Una máscara sin ningún metacarácter no sirve para nada: solo produciría literales.</summary>
    public static bool TieneMetacaracteres(string? mascara)
    {
        if (string.IsNullOrEmpty(mascara))
        {
            return false;
        }

        foreach (var ch in mascara)
        {
            if (EsMetacaracter(ch))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Cantidad de posiciones que el usuario debe teclear (los metacaracteres).</summary>
    public static int Longitud(string? mascara)
    {
        if (string.IsNullOrEmpty(mascara))
        {
            return 0;
        }

        var total = 0;
        foreach (var ch in mascara)
        {
            if (EsMetacaracter(ch))
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>
    /// Deja solo letras y dígitos. Es la forma en que el valor se guarda en la base
    /// cuando el formato tiene <c>normalizar</c> encendido.
    /// </summary>
    public static string Normalizar(string? valor, bool mayusculas = true)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(valor.Length);
        foreach (var ch in valor)
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(mayusculas ? char.ToUpperInvariant(ch) : ch);
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Coloca los literales de la máscara sobre el valor normalizado. Si el valor es más corto
    /// que la máscara se corta donde alcanza (sin dejar literales colgando); si es más largo,
    /// el sobrante se anexa al final para que el usuario vea que algo no cuadra.
    /// </summary>
    public static string Formatear(string? valor, string? mascara, bool mayusculas = true)
    {
        var normalizado = Normalizar(valor, mayusculas);
        if (normalizado.Length == 0)
        {
            return string.Empty;
        }

        if (!TieneMetacaracteres(mascara))
        {
            return normalizado;
        }

        var sb = new StringBuilder(mascara!.Length + normalizado.Length);
        var pendientes = new StringBuilder();
        var indice = 0;

        foreach (var ch in mascara)
        {
            if (EsMetacaracter(ch))
            {
                if (indice >= normalizado.Length)
                {
                    break;
                }

                sb.Append(pendientes);
                pendientes.Clear();
                sb.Append(normalizado[indice]);
                indice++;
                continue;
            }

            pendientes.Append(ch);
        }

        if (indice < normalizado.Length)
        {
            sb.Append(normalizado, indice, normalizado.Length - indice);
        }

        return sb.ToString();
    }

    /// <summary>
    /// Traduce la máscara de la casa a una máscara de texto de DevExpress
    /// (<c>DxMaskedInput</c>): <c>#</c> pasa a <c>0</c>, <c>X</c> y <c>H</c> pasan a <c>A</c>,
    /// y los literales que DevExpress interpretaría se escapan con barra invertida.
    /// </summary>
    public static string ToDevExpressMask(string? mascara)
    {
        if (string.IsNullOrEmpty(mascara))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(mascara.Length + 4);
        foreach (var ch in mascara)
        {
            switch (ch)
            {
                case MetaDigito:
                    sb.Append('0');
                    break;
                case MetaAlfanumerico:
                case MetaHexadecimal:
                    sb.Append('A');
                    break;
                default:
                    if (DevExpressReservados.IndexOf(ch) >= 0)
                    {
                        sb.Append('\\');
                    }

                    sb.Append(ch);
                    break;
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Deriva la expresión regular de la máscara agrupando las corridas del mismo metacarácter:
    /// <c>###-###-##-########</c> produce <c>^\d{3}-\d{3}-\d{2}-\d{8}$</c>.
    /// Se contrasta contra el valor <b>ya formateado</b> (con los literales puestos).
    /// </summary>
    public static string ToRegex(string? mascara)
    {
        if (!TieneMetacaracteres(mascara))
        {
            return string.Empty;
        }

        var sb = new StringBuilder("^");
        var corridaActual = '\0';
        var corridaLargo = 0;

        void CerrarCorrida()
        {
            if (corridaLargo == 0)
            {
                return;
            }

            sb.Append(corridaActual switch
            {
                MetaDigito => "\\d",
                MetaHexadecimal => "[0-9A-F]",
                _ => "[A-Z0-9]"
            });

            if (corridaLargo > 1)
            {
                sb.Append('{').Append(corridaLargo).Append('}');
            }

            corridaActual = '\0';
            corridaLargo = 0;
        }

        foreach (var ch in mascara!)
        {
            if (EsMetacaracter(ch))
            {
                if (corridaActual != ch)
                {
                    CerrarCorrida();
                    corridaActual = ch;
                }

                corridaLargo++;
                continue;
            }

            CerrarCorrida();
            sb.Append(Regex.Escape(ch.ToString()));
        }

        CerrarCorrida();
        sb.Append('$');
        return sb.ToString();
    }

    /// <summary>
    /// Ejemplo con la forma de la máscara, para usarlo como marcador de posición:
    /// <c>###-###-##-########</c> produce <c>000-000-00-00000000</c>.
    /// </summary>
    public static string Ejemplo(string? mascara)
    {
        if (string.IsNullOrEmpty(mascara))
        {
            return string.Empty;
        }

        var sb = new StringBuilder(mascara.Length);
        foreach (var ch in mascara)
        {
            sb.Append(ch switch
            {
                MetaDigito => '0',
                MetaAlfanumerico or MetaHexadecimal => 'A',
                _ => ch
            });
        }

        return sb.ToString();
    }

    /// <summary>
    /// Contrasta el valor contra el formato. El valor se normaliza y se formatea primero, así que
    /// da igual que el usuario lo haya tecleado con separadores o sin ellos. Si <paramref name="patron"/>
    /// viene con algo, se usa en lugar de la expresión derivada de la máscara — y también se aplica
    /// sobre el valor ya formateado.
    /// </summary>
    /// <remarks>Un valor vacío se considera válido: lo obligatorio lo decide el catálogo, no el formato.</remarks>
    public static bool EsValido(string? valor, string? mascara, string? patron = null, bool mayusculas = true)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return true;
        }

        var expresion = string.IsNullOrWhiteSpace(patron) ? ToRegex(mascara) : patron!.Trim();
        if (string.IsNullOrEmpty(expresion))
        {
            return true;
        }

        var formateado = Formatear(valor, mascara, mayusculas);

        try
        {
            return Regex.IsMatch(formateado, expresion, RegexOptions.CultureInvariant, RegexTimeout);
        }
        catch (ArgumentException)
        {
            // Patrón inválido tecleado en el mantenimiento: no se bloquea la captura por eso.
            return true;
        }
        catch (RegexMatchTimeoutException)
        {
            return true;
        }
    }
}
