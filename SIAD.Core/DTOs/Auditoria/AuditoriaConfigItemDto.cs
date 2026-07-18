namespace SIAD.Core.DTOs.Auditoria;

public sealed class AuditoriaConfigItemDto
{
    public string Entidad { get; set; } = string.Empty;   // tabla
    public string Nombre { get; set; } = string.Empty;    // amigable
    public string Modulo { get; set; } = string.Empty;
    public bool Habilitado { get; set; }
    public bool AuditaCrear { get; set; } = true;
    public bool AuditaEditar { get; set; } = true;
    public bool AuditaEliminar { get; set; } = true;
}
