using System.Globalization;
using System.Xml.Linq;

namespace apc.BancosWs.Contrato;

/// <summary>
/// Cuerpo XML de &lt;pago&gt; / &lt;reversion&gt; del contrato (orden de elementos
/// libre para la entrada, igual que JAXB). Los campos son strings crudos: las
/// validaciones de vacío replican el orden EXACTO del WS viejo y el parseo de
/// fecha/monto ocurre después.
/// </summary>
public sealed class PagoXmlRequest
{
    public string? Banco { get; private init; }
    public string? Cajero { get; private init; }
    public string? Clave { get; private init; }
    public string? Expediente { get; private init; }
    public string? FechaEfectiva { get; private init; }
    public string? FechaRegistro { get; private init; }
    public string? HoraRegistro { get; private init; }
    public string? Monto { get; private init; }
    public string? Referencia { get; private init; }
    public string? Sucursal { get; private init; }

    public static async Task<PagoXmlRequest?> LeerAsync(Stream body, CancellationToken ct)
    {
        try
        {
            var doc = await XDocument.LoadAsync(body, LoadOptions.None, ct);
            var root = doc.Root;
            if (root is null)
            {
                return null;
            }

            static string? Valor(XElement raiz, string nombre) => raiz.Element(nombre)?.Value;

            return new PagoXmlRequest
            {
                Banco = Valor(root, "banco"),
                Cajero = Valor(root, "cajero"),
                Clave = Valor(root, "clave"),
                Expediente = Valor(root, "expediente"),
                FechaEfectiva = Valor(root, "fechaEfectiva"),
                FechaRegistro = Valor(root, "fechaRegistro"),
                HoraRegistro = Valor(root, "horaRegistro"),
                Monto = Valor(root, "monto"),
                Referencia = Valor(root, "referencia"),
                Sucursal = Valor(root, "sucursal"),
            };
        }
        catch (System.Xml.XmlException)
        {
            return null;
        }
    }

    public bool TryParseMonto(out decimal monto) =>
        decimal.TryParse(Monto, NumberStyles.Number, CultureInfo.InvariantCulture, out monto);

    public bool TryParseFechaRegistro(out DateOnly fecha) =>
        DateOnly.TryParseExact(FechaRegistro, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    public TimeOnly? ParseHoraRegistro() =>
        TimeOnly.TryParseExact(HoraRegistro, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora)
            ? hora
            : null;

    public DateOnly? ParseFechaEfectiva() =>
        DateOnly.TryParseExact(FechaEfectiva, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha)
            ? fecha
            : null;
}
