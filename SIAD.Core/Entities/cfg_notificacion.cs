using System;
using System.Collections.Generic;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Área/tipo de notificación por empresa (N filas). Un renglón por tipo del catálogo
/// (<see cref="SIAD.Core.Constants.TipoNotificacion"/>): remitente propio (opcional) y sus
/// destinatarios. El tipo lo define el CÓDIGO (quien dispara el evento), no el usuario; la
/// pantalla solo asigna remitente y destinatarios.
/// </summary>
public partial class cfg_notificacion : ICompanyScopedEntity
{
    public long id { get; set; }

    public long company_id { get; set; }

    /// <summary>ADMINISTRACION | ALMACEN | COBRANZA | SISTEMA. Espejo del CHECK y del catálogo.</summary>
    public string tipo { get; set; } = null!;

    /// <summary>Etiqueta editable, solo presentación.</summary>
    public string? nombre { get; set; }

    /// <summary>Override del remitente. NULL = usa el remitente por defecto de <c>cfg_correo</c>.</summary>
    public string? remitente_email { get; set; }

    public string? remitente_nombre { get; set; }

    /// <summary>Enciende/apaga este tipo sin borrarlo.</summary>
    public bool activo { get; set; } = true;

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }

    public ICollection<cfg_notificacion_destinatario> destinatarios { get; set; }
        = new List<cfg_notificacion_destinatario>();
}
