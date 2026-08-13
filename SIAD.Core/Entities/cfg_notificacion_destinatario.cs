using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Destinatario de un área de notificación (N por <c>cfg_notificacion</c>). Cada fila es un
/// correo destino, clasificado como TO o CC (<see cref="SIAD.Core.Constants.ClaseDestinatario"/>).
/// Los destinatarios se reemplazan como conjunto al guardar el área; por eso solo llevan
/// auditoría de creación.
/// </summary>
public partial class cfg_notificacion_destinatario : ICompanyScopedEntity
{
    public long id { get; set; }

    public long company_id { get; set; }

    /// <summary>FK a <c>cfg_notificacion</c>. ON DELETE CASCADE: al borrar el área, se van sus destinos.</summary>
    public long notificacion_id { get; set; }

    public string correo { get; set; } = null!;

    /// <summary>TO | CC.</summary>
    public string clase { get; set; } = "TO";

    public bool activo { get; set; } = true;

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }

    public cfg_notificacion? notificacion { get; set; }
}
