using System;
using SIAD.Core.Tenancy;

namespace SIAD.Core.Entities;

/// <summary>
/// Conexión de correo por empresa (una fila por proveedor por empresa). Es el TRANSPORTE:
/// la API key autentica la cuenta del proveedor (SendGrid); el remitente y el destinatario
/// van por mensaje, no por credencial, así que basta UNA conexión por empresa.
/// <para>
/// El enrutamiento por área (administración, almacén, …) vive en <c>cfg_notificacion</c> +
/// <c>cfg_notificacion_destinatario</c>, no aquí.
/// </para>
/// </summary>
public partial class cfg_correo : ICompanyScopedEntity
{
    public long id { get; set; }

    public long company_id { get; set; }

    /// <summary>SENDGRID | SMTP. Ver <see cref="SIAD.Core.Constants.ProveedorCorreo"/>.</summary>
    public string proveedor { get; set; } = "SENDGRID";

    /// <summary>
    /// API key CIFRADA con DataProtection (ciphertext base64url). Nunca se guarda en claro y
    /// nunca se devuelve al cliente: se descifra solo en memoria, al enviar.
    /// </summary>
    public string? api_key_cifrada { get; set; }

    /// <summary>Remitente por defecto; se usa cuando un área no define el suyo.</summary>
    public string? remitente_email_default { get; set; }

    public string? remitente_nombre_default { get; set; }

    /// <summary>Interruptor GLOBAL de envío. En false, no sale ningún correo.</summary>
    public bool activo { get; set; }

    public string? usuariocreacion { get; set; }
    public DateTime? fechacreacion { get; set; }
    public string? usuariomodificacion { get; set; }
    public DateTime? fechamodificacion { get; set; }
}
