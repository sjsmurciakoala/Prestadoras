namespace SIAD.Services.Auditoria;

public interface IAuditConfigProvider
{
    /// <summary>¿Se audita esta acción sobre esta tabla, para esta empresa?</summary>
    bool DebeAuditar(long companyId, string tabla, string accion); // accion: CREAR|EDITAR|ELIMINAR
    void Invalidar(long companyId);
}
