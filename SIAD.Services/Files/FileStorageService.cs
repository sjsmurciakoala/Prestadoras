using Microsoft.Extensions.Logging;

namespace SIAD.Services.Files;

/// <summary>
/// Implementación del servicio de almacenamiento de archivos.
/// </summary>
public sealed class FileStorageService : IFileStorageService
{
    private readonly ILogger<FileStorageService> _logger;
    private readonly string _uploadsBasePath;
    private static readonly string[] AllowedImageExtensions = { ".png", ".jpg", ".jpeg", ".gif", ".svg" };
    private static readonly string[] AllowedContentTypes =
    {
        "image/png",
        "image/jpeg",
        "image/gif",
        "image/svg+xml"
    };

    public FileStorageService(ILogger<FileStorageService> logger, string uploadsBasePath)
    {
        _logger = logger;
        _uploadsBasePath = uploadsBasePath;
    }

    public async Task<string> SaveCompanyLogoAsync(long companyId, Stream fileStream, string fileName, string contentType, CancellationToken ct = default)
    {
        if (!IsValidImageFile(fileName, contentType))
        {
            throw new InvalidOperationException("El archivo no es una imagen válida. Solo se permiten: PNG, JPG, GIF, SVG.");
        }

        // Crear directorio para la empresa si no existe
        var companyFolder = Path.Combine(_uploadsBasePath, "company-logos", companyId.ToString());
        Directory.CreateDirectory(companyFolder);

        // Eliminar logo anterior si existe
        await DeleteCompanyLogoAsync(companyId, ct);

        // Generar nombre del archivo con extensión
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var newFileName = $"logo{extension}";
        var filePath = Path.Combine(companyFolder, newFileName);

        // Guardar archivo
        using (var fileWriteStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
        {
            await fileStream.CopyToAsync(fileWriteStream, ct);
        }

        _logger.LogInformation("Logo guardado para empresa {CompanyId}: {FilePath}", companyId, filePath);

        // Retornar URL relativa
        return $"/uploads/company-logos/{companyId}/{newFileName}";
    }

    public Task<bool> DeleteCompanyLogoAsync(long companyId, CancellationToken ct = default)
    {
        try
        {
            var companyFolder = Path.Combine(_uploadsBasePath, "company-logos", companyId.ToString());
            if (!Directory.Exists(companyFolder))
            {
                return Task.FromResult(false);
            }

            // Eliminar todos los archivos de logo
            var logoFiles = Directory.GetFiles(companyFolder, "logo.*");
            foreach (var file in logoFiles)
            {
                File.Delete(file);
                _logger.LogInformation("Logo eliminado: {FilePath}", file);
            }

            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error al eliminar logo de empresa {CompanyId}", companyId);
            return Task.FromResult(false);
        }
    }

    public bool IsValidImageFile(string fileName, string contentType)
    {
        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var isValidExtension = AllowedImageExtensions.Contains(extension);
        var isValidContentType = AllowedContentTypes.Contains(contentType.ToLowerInvariant());

        return isValidExtension && isValidContentType;
    }
}
