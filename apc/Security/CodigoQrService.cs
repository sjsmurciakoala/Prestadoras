using DevExpress.XtraPrinting.BarCode;
using DevExpress.XtraReports.UI;

namespace apc.Security;

/// <summary>
/// Dibuja códigos QR en el servidor y los devuelve como <c>data:</c> URI.
///
/// Existe por la verificación en dos pasos: la plantilla de ASP.NET Identity deja el QR sin
/// implementar (solo un enlace «Learn how to enable QR code generation») y muestra la clave en
/// texto, que hay que teclear a mano. Aquí se genera de verdad.
///
/// Se hace en el servidor a propósito. Las pantallas de <c>/Account/*</c> se renderizan en modo
/// estático —sin circuito de Blazor—, así que una librería JavaScript no correría; y el
/// generador de códigos de barras ya viene con <c>DevExpress.Reporting.Core</c>, que el portal
/// referencia para la reportería, así que no se agrega ninguna dependencia.
/// </summary>
public sealed class CodigoQrService
{
    private readonly ILogger<CodigoQrService> _log;

    public CodigoQrService(ILogger<CodigoQrService> log) => _log = log;

    /// <summary>
    /// Devuelve el QR del texto indicado como <c>data:image/png;base64,…</c>, listo para el
    /// <c>src</c> de un <c>&lt;img&gt;</c>. Devuelve <c>null</c> si no se pudo generar: el QR es
    /// una comodidad y su ausencia no debe tumbar la pantalla, que siempre ofrece la clave
    /// escrita como alternativa.
    /// </summary>
    public string? GenerarDataUri(string texto, int lado = 200)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        try
        {
            using var codigo = new XRBarCode
            {
                Symbology = new QRCodeGenerator(),
                Text = texto,
                Width = lado,
                Height = lado,
                AutoModule = true,
                ShowText = false,
                Padding = new DevExpress.XtraPrinting.PaddingInfo(4, 4, 4, 4, 96),
            };

            // Byte: el otpauth:// lleva minúsculas, ':' y '/', que el modo alfanumérico no admite.
            ((QRCodeGenerator)codigo.Symbology).CompactionMode = QRCodeCompactionMode.Byte;
            ((QRCodeGenerator)codigo.Symbology).ErrorCorrectionLevel = QRCodeErrorCorrectionLevel.M;
            ((QRCodeGenerator)codigo.Symbology).Version = QRCodeVersion.AutoVersion;

            using var reporte = new XtraReport
            {
                PaperKind = DevExpress.Drawing.Printing.DXPaperKind.Custom,
                PageWidth = lado,
                PageHeight = lado,
                // Sin márgenes: la página es exactamente el cuadro del código. Hay que fijarlos
                // en cero explícitamente; los de por defecto (100) superan una página de 200 px
                // y el reporte falla con «Page margins are greater than page size».
                Margins = new DevExpress.Drawing.DXMargins(0, 0, 0, 0),
                Bands = { new DetailBand { HeightF = lado, Controls = { codigo } } },
            };

            using var memoria = new MemoryStream();
            reporte.ExportToImage(memoria, new DevExpress.XtraPrinting.ImageExportOptions
            {
                Format = DevExpress.Drawing.DXImageFormat.Png,
                Resolution = 96,
                ExportMode = DevExpress.XtraPrinting.ImageExportMode.SingleFile,
            });

            return "data:image/png;base64," + Convert.ToBase64String(memoria.ToArray());
        }
        catch (Exception ex)
        {
            // Sin QR se sigue pudiendo configurar el autenticador escribiendo la clave, pero
            // que falle en silencio deja la pantalla a medias sin explicar por qué.
            _log.LogWarning(ex, "No se pudo generar el código QR del autenticador.");
            return null;
        }
    }
}
