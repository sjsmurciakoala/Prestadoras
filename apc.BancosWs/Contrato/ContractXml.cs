using System.Globalization;
using System.Text;
using SIAD.Core.DTOs.BancosWs;

namespace apc.BancosWs.Contrato;

/// <summary>
/// Emisor XML byte-exacto del contrato SIMAFI (docs/f8-contrato-ws-bancario.md).
/// Se emite a mano (no XmlSerializer) para controlar cada byte: declaración
/// idéntica a JAXB (encoding="UTF-8" standalone="yes"), una sola línea sin
/// pretty-print (marshaller JAXB de GlassFish sin JAXB_FORMATTED_OUTPUT),
/// elementos vacíos en forma larga (&lt;estado&gt;&lt;/estado&gt; — XmlWriter los
/// colapsaría a &lt;estado /&gt;) y orden de elementos alfabético (§5.1 del
/// contrato: hipótesis JAXB confirmable con una captura real en el cutover —
/// ajustar SOLO acá si la captura difiere).
/// </summary>
public static class ContractXml
{
    public const string ContentTypeXml = "application/xml";

    /// <summary>Content-Type del rechazo del filtro de auth (servlet GlassFish sin setContentType).</summary>
    public const string ContentTypeFiltro = "text/html;charset=ISO-8859-1";

    private const string Declaracion = "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>";

    // ----- Mensajes EXACTOS del contrato (no tocar ni "corregir" tildes/typos) -----
    public const string MsgNoExisteRegistro = "No existe registro";
    public const string MsgNoHayPagosPendientes = "No hay pagos pendientes";
    public const string MsgFacturasVencidas = "Las facturas estan vencidas";
    public const string MsgProblemaServidor = "Problema con el Web servidor";
    public const string MsgNoExisteRegistroPunto = "No existe registro.";
    public const string MsgNoHayPagosPendientesPunto = "No hay pagos pendientes.";
    public const string MsgTotalNoCoincide = "Total a pagar no coincide con el monto.";
    public const string MsgNoSePuedePagar = "No se puede pagar, revisar log de Servidor";
    public const string MsgPagoExitoso = "Pago exitoso";
    public const string MsgFechaRegistroVacia = "Fecha de registro no puede estar vacia";
    public const string MsgReferenciaVacia = "Referencia no puede estar vacia";
    public const string MsgBancoVacio = "Codigo de banco puede estar vacio";
    public const string MsgClaveVacia = "Clave no puede estar vacio";
    public const string MsgNoExisteReferenciaReversar = "No existe numero de referencia, para reversar";
    public const string MsgReferenciaYaReversada = "Numero de referencia ya fue reversado";
    public const string MsgNoSePuedeReversar = "No se puede reversar";
    public const string MsgReversionExitosa = "Reversion exitosa";
    public const string MsgNoAutorizado = "No autorizado";
    public const string MsgLlaveActualizada = "Llave actualizada";
    public const string MsgNoSePuedeActualizarLlave = "No se puede actualizar llave";
    public const string MsgNoExisteRegistroBanco = "No existe registro de banco";
    public const string HeartbeatBody = "ok ok";

    /// <summary>&lt;mensaje&gt; JAXB (orden alfabético: error, estado, mensaje) con declaración.</summary>
    public static string Mensaje(int estado, bool error, string mensaje)
    {
        var sb = new StringBuilder(160);
        sb.Append(Declaracion);
        sb.Append("<mensaje>");
        sb.Append("<error>").Append(error ? "true" : "false").Append("</error>");
        sb.Append("<estado>").Append(estado.ToString(CultureInfo.InvariantCulture)).Append("</estado>");
        Elemento(sb, "mensaje", mensaje);
        sb.Append("</mensaje>");
        return sb.ToString();
    }

    /// <summary>
    /// Cuerpo del rechazo del filtro de auth: Handle.toString() del WS viejo —
    /// orden estado, error, mensaje, SIN declaración XML, con salto de línea
    /// final (println de PrintWriter en Windows). Verificado en LOG_SIMAFI.log.
    /// </summary>
    public static string MensajeFiltroNoAutorizado() =>
        "<mensaje><estado>400</estado><error>false</error><mensaje>No autorizado</mensaje></mensaje>\r\n";

    /// <summary>&lt;factura&gt; de la consulta (cabecera de la primera fila + un &lt;detalle&gt; por línea).</summary>
    public static string Factura(BancosWsConsultaDto consulta)
    {
        var sb = new StringBuilder(1024);
        sb.Append(Declaracion);
        sb.Append("<factura>");

        // Cabecera — orden alfabético JAXB; ano/expediente/mes nunca se llenan
        // en servicios (omitidos); comentario/direccion se omiten si NULL.
        // estado: en SIMAFI las filas pendientes tienen estado = '' → elemento
        // presente y VACÍO en forma larga.
        sb.Append("<cabecera>");
        Elemento(sb, "clave", consulta.Clave);
        if (!string.IsNullOrEmpty(consulta.Direccion))
        {
            Elemento(sb, "direccion", consulta.Direccion);
        }
        sb.Append("<estado></estado>");
        Elemento(sb, "fechaVence", consulta.FechaVence);
        Elemento(sb, "nombreAbonado", consulta.Nombre);
        Elemento(sb, "totalMora", Monto(consulta.Total));
        sb.Append("</cabecera>");

        foreach (var detalle in consulta.Detalles)
        {
            sb.Append("<detalle>");
            Elemento(sb, "codigoConcepto", detalle.CodigoConcepto);
            Elemento(sb, "concepto", detalle.Concepto);
            Elemento(sb, "id", detalle.Id.ToString(CultureInfo.InvariantCulture));
            Elemento(sb, "valor", Monto(detalle.Valor));
            sb.Append("</detalle>");
        }

        sb.Append("</factura>");
        return sb.ToString();
    }

    /// <summary>&lt;pago&gt; del endpoint de prueba GET /pago/dummy (valores fijos del WS viejo).</summary>
    public static string PagoDummy()
    {
        var sb = new StringBuilder(512);
        sb.Append(Declaracion);
        sb.Append("<pago>");
        Elemento(sb, "banco", "001");
        Elemento(sb, "cajero", "JOSEFO");
        Elemento(sb, "clave", "GTA2-1035");
        Elemento(sb, "expediente", "5050");
        Elemento(sb, "fechaEfectiva", "2012-01-10");
        Elemento(sb, "horaRegistro", "10:20:20");
        Elemento(sb, "monto", "1200.0");
        Elemento(sb, "referencia", "REF1122334455");
        Elemento(sb, "sucursal", "01");
        sb.Append("</pago>");
        return sb.ToString();
    }

    /// <summary>Formato de montos del contrato: 2 decimales, punto decimal, sin miles (decimal(12,2) SIMAFI).</summary>
    public static string Monto(decimal valor) =>
        valor.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Elemento(StringBuilder sb, string nombre, string? valor)
    {
        sb.Append('<').Append(nombre).Append('>');
        Escapar(sb, valor);
        sb.Append("</").Append(nombre).Append('>');
    }

    // Escapado JAXB: &, <, > en contenido de texto (\r → &#13; para no perderlo).
    private static void Escapar(StringBuilder sb, string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return;
        }

        foreach (var c in valor)
        {
            switch (c)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '\r': sb.Append("&#13;"); break;
                default: sb.Append(c); break;
            }
        }
    }
}
