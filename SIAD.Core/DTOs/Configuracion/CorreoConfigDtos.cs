using System.Collections.Generic;

namespace SIAD.Core.DTOs.Configuracion;

/// <summary>
/// Conexión de correo (lectura). <b>NUNCA</b> lleva la API key: solo indica si hay una
/// configurada (<see cref="TieneApiKey"/>).
/// </summary>
public sealed class ConexionCorreoDto
{
    public string Proveedor { get; set; } = "SENDGRID";
    public string? RemitenteEmailDefault { get; set; }
    public string? RemitenteNombreDefault { get; set; }
    public bool Activo { get; set; }
    public bool TieneApiKey { get; set; }
}

/// <summary>Conexión de correo (escritura).</summary>
public sealed class ConexionCorreoUpsertDto
{
    public string Proveedor { get; set; } = "SENDGRID";
    public string? RemitenteEmailDefault { get; set; }
    public string? RemitenteNombreDefault { get; set; }
    public bool Activo { get; set; }

    /// <summary>
    /// Si viene con valor, reemplaza la API key (se cifra al guardar). Vacío o nulo = conservar
    /// la actual. La clave almacenada nunca se devuelve, así que este campo es de solo escritura.
    /// </summary>
    public string? NuevaApiKey { get; set; }
}

/// <summary>Destinatario de un área (TO/CC).</summary>
public sealed class DestinatarioCorreoDto
{
    public string Correo { get; set; } = string.Empty;
    public string Clase { get; set; } = "TO";
    public bool Activo { get; set; } = true;
}

/// <summary>
/// Área/tipo de notificación (lectura y escritura). Al guardar, la lista de destinatarios se
/// reemplaza como conjunto; el upsert se resuelve por <see cref="Tipo"/> (único por empresa).
/// </summary>
public sealed class NotificacionCorreoDto
{
    public long Id { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string? Nombre { get; set; }
    public string? RemitenteEmail { get; set; }
    public string? RemitenteNombre { get; set; }
    public bool Activo { get; set; } = true;
    public List<DestinatarioCorreoDto> Destinatarios { get; set; } = new();
}

/// <summary>
/// Resultado de resolver el envío de un tipo de notificación: la API key <b>descifrada</b>, el
/// remitente efectivo (override del área → default de la conexión) y los destinatarios activos.
/// <b>Uso interno del sender</b> — nunca se serializa por HTTP.
/// </summary>
public sealed class EnvioCorreoResueltoDto
{
    public string Proveedor { get; set; } = "SENDGRID";
    public string? ApiKey { get; set; }
    public string? RemitenteEmail { get; set; }
    public string? RemitenteNombre { get; set; }
    public List<string> Para { get; set; } = new();
    public List<string> ConCopia { get; set; } = new();
}
