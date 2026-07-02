namespace SIAD.Core.DTOs.Cobranza;

/// <summary>Códigos de documento soportados por el generador.</summary>
public static class DocumentosCobranzaCodigos
{
    public const string CartaCobranzaPrejudicial = "CARTA_COBRANZA_PREJUDICIAL";

    /// <summary>Catálogo de códigos disponibles para el combo de mantenimiento.</summary>
    public static readonly IReadOnlyList<DocumentoCobranzaOpcionDto> Disponibles =
    [
        new(CartaCobranzaPrejudicial, "Carta de Cobranza Prejudicial")
    ];
}

public record DocumentoCobranzaOpcionDto(string Codigo, string Nombre);

/// <summary>Datos que el servicio arma y entrega al generador para producir el documento.</summary>
public record DocumentoCobranzaDatos(
    string ClienteClave,
    string ClienteNombre,
    string? Direccion,
    decimal TotalAdeudado,
    string? Firmante,
    DateTime FechaEmision,
    int PlazoDias);

/// <summary>Resultado de la generación: archivo listo para archivar/entregar.</summary>
public record DocumentoGenerado(string NombreArchivo, byte[] Contenido, string ContentType);

/// <summary>
/// Genera el documento (PDF) de una acción de cobranza. La implementación
/// (DevExpress) vive en SIAD.Reports; el servicio depende solo de esta interfaz.
/// </summary>
public interface IDocumentoCobranzaGenerator
{
    bool Soporta(string documentoCodigo);

    DocumentoGenerado Generar(string documentoCodigo, DocumentoCobranzaDatos datos);
}
