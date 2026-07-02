using System.Text;
using SIAD.Core.DTOs.Cobranza;

namespace SIAD.Tests.Fakes;

/// <summary>
/// Generador de documentos de prueba: no usa DevExpress, devuelve bytes deterministas
/// para poder verificar el archivado del snapshot sin depender del render real.
/// </summary>
public sealed class FakeDocumentoCobranzaGenerator : IDocumentoCobranzaGenerator
{
    public bool Soporta(string documentoCodigo) => true;

    public DocumentoGenerado Generar(string documentoCodigo, DocumentoCobranzaDatos datos)
    {
        var contenido = Encoding.UTF8.GetBytes(
            $"FAKE-PDF|{documentoCodigo}|{datos.ClienteClave}|{datos.TotalAdeudado}");
        return new DocumentoGenerado(
            $"{documentoCodigo}-{datos.ClienteClave}.pdf", contenido, "application/pdf");
    }
}
