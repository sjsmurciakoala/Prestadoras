using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Configuracion;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Configuracion;

/// <summary>
/// Configuración de correo por empresa (cfg_correo + cfg_notificacion + cfg_notificacion_destinatario).
/// El tenant lo resuelve el filtro global del contexto; el <c>company_id</c> lo estampa
/// <c>SaveChanges</c>. La API key se cifra/descifra con DataProtection (protector
/// <c>cfg_correo.apikey</c>) y nunca sale de este servicio en claro salvo por
/// <see cref="ResolverEnvioAsync"/> (uso del sender).
/// </summary>
public sealed class CorreoConfigService : ICorreoConfigService, ICorreoEnvioResolver
{
    private readonly SiadDbContext _context;
    private readonly IDataProtector _protector;

    public CorreoConfigService(SiadDbContext context, IDataProtectionProvider dataProtection)
    {
        _context = context;
        _protector = dataProtection.CreateProtector("cfg_correo.apikey");
    }

    // ─────────────────────────────────────────────────────────── conexión

    public async Task<ConexionCorreoDto> ObtenerConexionAsync(CancellationToken ct = default)
    {
        var cfg = await _context.cfg_correos.AsNoTracking().FirstOrDefaultAsync(ct);
        return new ConexionCorreoDto
        {
            Proveedor = cfg?.proveedor ?? ProveedorCorreo.SendGrid,
            RemitenteEmailDefault = cfg?.remitente_email_default,
            RemitenteNombreDefault = cfg?.remitente_nombre_default,
            Activo = cfg?.activo ?? false,
            TieneApiKey = !string.IsNullOrEmpty(cfg?.api_key_cifrada)
        };
    }

    public async Task<ConexionCorreoDto> GuardarConexionAsync(ConexionCorreoUpsertDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var proveedor = (dto.Proveedor ?? string.Empty).Trim().ToUpperInvariant();
        if (!ProveedorCorreo.EsValido(proveedor))
            throw new InvalidOperationException("El proveedor de correo debe ser SENDGRID o SMTP.");

        var ahora = Ahora();
        var usuario = NormalizarUsuario(user);

        var cfg = await _context.cfg_correos.FirstOrDefaultAsync(ct);
        var esNuevo = cfg is null;
        cfg ??= new cfg_correo();

        // ¿Quedará una API key configurada tras guardar? (la nueva, o la que ya había)
        var traeKeyNueva = !string.IsNullOrWhiteSpace(dto.NuevaApiKey);
        var tendraKey = traeKeyNueva || !string.IsNullOrEmpty(cfg.api_key_cifrada);
        if (dto.Activo && !tendraKey)
            throw new InvalidOperationException("No se puede activar el envío sin una API key configurada.");

        cfg.proveedor = proveedor;
        cfg.remitente_email_default = Limpiar(dto.RemitenteEmailDefault, 200);
        cfg.remitente_nombre_default = Limpiar(dto.RemitenteNombreDefault, 150);
        cfg.activo = dto.Activo;
        if (traeKeyNueva)
            cfg.api_key_cifrada = _protector.Protect(dto.NuevaApiKey!.Trim());

        if (esNuevo)
        {
            cfg.usuariocreacion = usuario;
            cfg.fechacreacion = ahora;
            _context.cfg_correos.Add(cfg);
        }
        else
        {
            cfg.usuariomodificacion = usuario;
            cfg.fechamodificacion = ahora;
        }

        await _context.SaveChangesAsync(ct);
        return await ObtenerConexionAsync(ct);
    }

    // ─────────────────────────────────────────────────────── notificaciones

    public async Task<IReadOnlyList<NotificacionCorreoDto>> ListarNotificacionesAsync(CancellationToken ct = default)
    {
        var areas = await _context.cfg_notificacions.AsNoTracking()
            .Include(n => n.destinatarios)
            .OrderBy(n => n.tipo)
            .ToListAsync(ct);

        return areas.Select(ToDto).ToList();
    }

    public async Task<NotificacionCorreoDto> GuardarNotificacionAsync(NotificacionCorreoDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var tipo = (dto.Tipo ?? string.Empty).Trim().ToUpperInvariant();
        if (!TipoNotificacion.EsValido(tipo))
            throw new InvalidOperationException("El tipo de notificación no es válido.");

        // Normalizar y validar destinatarios antes de tocar la BD.
        var destinos = (dto.Destinatarios ?? new List<DestinatarioCorreoDto>());
        foreach (var d in destinos)
        {
            d.Correo = (d.Correo ?? string.Empty).Trim();
            d.Clase = (d.Clase ?? ClaseDestinatario.To).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(d.Correo))
                throw new InvalidOperationException("Hay un destinatario sin correo.");
            if (!ClaseDestinatario.EsValida(d.Clase))
                throw new InvalidOperationException($"La clase del destinatario '{d.Correo}' debe ser TO o CC.");
        }

        var ahora = Ahora();
        var usuario = NormalizarUsuario(user);

        var area = await _context.cfg_notificacions
            .Include(n => n.destinatarios)
            .FirstOrDefaultAsync(n => n.tipo == tipo, ct);

        if (area is null)
        {
            area = new cfg_notificacion { tipo = tipo, usuariocreacion = usuario, fechacreacion = ahora };
            _context.cfg_notificacions.Add(area);
        }
        else
        {
            area.usuariomodificacion = usuario;
            area.fechamodificacion = ahora;
            // Los destinatarios se reemplazan como conjunto: borrar los actuales y re-crear.
            _context.cfg_notificacion_destinatarios.RemoveRange(area.destinatarios);
            area.destinatarios.Clear();
        }

        area.nombre = Limpiar(dto.Nombre, 120);
        area.remitente_email = Limpiar(dto.RemitenteEmail, 200);
        area.remitente_nombre = Limpiar(dto.RemitenteNombre, 150);
        area.activo = dto.Activo;

        // Deduplicar por (clase, correo normalizado) para no violar uq_cfg_notif_dest_correo.
        var vistos = new HashSet<string>();
        foreach (var d in destinos)
        {
            if (!vistos.Add(d.Clase + "|" + d.Correo.ToLowerInvariant()))
                continue;
            area.destinatarios.Add(new cfg_notificacion_destinatario
            {
                correo = d.Correo,
                clase = d.Clase,
                activo = d.Activo,
                usuariocreacion = usuario,
                fechacreacion = ahora
            });
        }

        await _context.SaveChangesAsync(ct);

        return (await ListarNotificacionesAsync(ct)).First(n => n.Tipo == tipo);
    }

    // ─────────────────────────────────────────────────────── resolver (sender)

    public async Task<EnvioCorreoResueltoDto?> ResolverEnvioAsync(string tipo, CancellationToken ct = default)
    {
        var tipoNorm = (tipo ?? string.Empty).Trim().ToUpperInvariant();

        var conexion = await _context.cfg_correos.AsNoTracking().FirstOrDefaultAsync(ct);
        if (conexion is null || !conexion.activo)
            return null; // envío global apagado o sin conexión

        var area = await _context.cfg_notificacions.AsNoTracking()
            .Include(n => n.destinatarios)
            .FirstOrDefaultAsync(n => n.tipo == tipoNorm, ct);
        if (area is null || !area.activo)
            return null;

        string? apiKey = null;
        if (!string.IsNullOrEmpty(conexion.api_key_cifrada))
        {
            try { apiKey = _protector.Unprotect(conexion.api_key_cifrada); }
            catch (CryptographicException) { apiKey = null; } // key-ring cambió → no se puede descifrar
        }

        var activos = area.destinatarios.Where(d => d.activo).ToList();
        return new EnvioCorreoResueltoDto
        {
            Proveedor = conexion.proveedor,
            ApiKey = apiKey,
            RemitenteEmail = string.IsNullOrWhiteSpace(area.remitente_email)
                ? conexion.remitente_email_default : area.remitente_email,
            RemitenteNombre = string.IsNullOrWhiteSpace(area.remitente_nombre)
                ? conexion.remitente_nombre_default : area.remitente_nombre,
            Para = activos.Where(d => d.clase == ClaseDestinatario.To).Select(d => d.correo).ToList(),
            ConCopia = activos.Where(d => d.clase == ClaseDestinatario.Cc).Select(d => d.correo).ToList()
        };
    }

    public async Task<EnvioCorreoResueltoDto?> ResolverTransporteAsync(long companyId, string tipoRemitente, CancellationToken ct = default)
    {
        if (companyId <= 0) return null;
        var tipoNorm = (tipoRemitente ?? string.Empty).Trim().ToUpperInvariant();

        // Cross-tenant DOCUMENTADO: los correos de sistema (Identity) se envían sin sesión, así que la
        // empresa NO es la "actual" — se recibe explícita y se ignora el filtro global de tenant.
        var conexion = await _context.cfg_correos.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
        if (conexion is null || !conexion.activo || string.IsNullOrEmpty(conexion.api_key_cifrada))
            return null;

        string? apiKey;
        try { apiKey = _protector.Unprotect(conexion.api_key_cifrada); }
        catch (CryptographicException) { return null; } // key-ring cambió → no se puede descifrar
        if (string.IsNullOrEmpty(apiKey)) return null;

        // El área del tipo (si existe) aporta el remitente override; si está inactiva, apaga ese tipo.
        var area = await _context.cfg_notificacions.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(n => n.company_id == companyId && n.tipo == tipoNorm, ct);
        if (area is not null && !area.activo)
            return null;

        var remitenteEmail = string.IsNullOrWhiteSpace(area?.remitente_email)
            ? conexion.remitente_email_default : area!.remitente_email;
        if (string.IsNullOrWhiteSpace(remitenteEmail))
            return null; // sin remitente no se puede enviar

        return new EnvioCorreoResueltoDto
        {
            Proveedor = conexion.proveedor,
            ApiKey = apiKey,
            RemitenteEmail = remitenteEmail,
            RemitenteNombre = string.IsNullOrWhiteSpace(area?.remitente_nombre)
                ? conexion.remitente_nombre_default : area!.remitente_nombre
        };
    }

    public async Task<EnvioCorreoResueltoDto?> ResolverPruebaAsync(CancellationToken ct = default)
    {
        // Empresa ACTUAL (filtro global). Probar NO respeta el interruptor 'activo': es explícito.
        var conexion = await _context.cfg_correos.AsNoTracking().FirstOrDefaultAsync(ct);
        if (conexion is null || string.IsNullOrEmpty(conexion.api_key_cifrada))
            return null;

        string? apiKey;
        try { apiKey = _protector.Unprotect(conexion.api_key_cifrada); }
        catch (CryptographicException) { return null; }
        if (string.IsNullOrEmpty(apiKey)) return null;

        if (string.IsNullOrWhiteSpace(conexion.remitente_email_default))
            return null; // sin remitente no se puede enviar la prueba

        return new EnvioCorreoResueltoDto
        {
            Proveedor = conexion.proveedor,
            ApiKey = apiKey,
            RemitenteEmail = conexion.remitente_email_default,
            RemitenteNombre = conexion.remitente_nombre_default
        };
    }

    // ─────────────────────────────────────────────────────────── helpers

    private static NotificacionCorreoDto ToDto(cfg_notificacion n) => new()
    {
        Id = n.id,
        Tipo = n.tipo,
        Nombre = n.nombre,
        RemitenteEmail = n.remitente_email,
        RemitenteNombre = n.remitente_nombre,
        Activo = n.activo,
        Destinatarios = n.destinatarios
            .OrderBy(d => d.clase).ThenBy(d => d.correo)
            .Select(d => new DestinatarioCorreoDto { Correo = d.correo, Clase = d.clase, Activo = d.activo })
            .ToList()
    };

    private static DateTime Ahora() => DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

    private static string NormalizarUsuario(string? user)
    {
        var u = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        return u.Length > 100 ? u[..100] : u;
    }

    private static string? Limpiar(string? valor, int max)
    {
        if (string.IsNullOrWhiteSpace(valor)) return null;
        var v = valor.Trim();
        return v.Length > max ? v[..max] : v;
    }
}
