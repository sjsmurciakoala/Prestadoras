namespace SIAD.Services.Files;

/// <summary>
/// Servicio para gestionar almacenamiento de archivos (logos, documentos, etc.)
/// </summary>
public interface IFileStorageService
{
    /// <summary>
    /// Guarda un logo de empresa y retorna la URL relativa.
    /// </summary>
    /// <param name="companyId">ID de la empresa</param>
    /// <param name="fileStream">Stream del archivo</param>
    /// <param name="fileName">Nombre del archivo original</param>
    /// <param name="contentType">Tipo de contenido (image/png, image/svg+xml, etc.)</param>
    /// <returns>URL relativa del archivo guardado</returns>
    Task<string> SaveCompanyLogoAsync(long companyId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Elimina el logo de una empresa.
    /// </summary>
    Task<bool> DeleteCompanyLogoAsync(long companyId, CancellationToken ct = default);

    /// <summary>
    /// Valida que el archivo sea una imagen válida (PNG, JPG, GIF, SVG).
    /// </summary>
    bool IsValidImageFile(string fileName, string contentType);
}
