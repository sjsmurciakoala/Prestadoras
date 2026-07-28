namespace SIAD.Services.Auditoria;

// Catálogo por empresa de tablas auditables (leído de bitacora_maestro_catalogo, cacheado).
public interface IAuditableCatalogProvider
{
    bool EsAuditable(long companyId, string tabla);
    string NombreDe(long companyId, string tabla);
    string ModuloDe(long companyId, string tabla);
    void Invalidar(long companyId);
}
