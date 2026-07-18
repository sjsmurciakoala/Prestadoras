using System;
using System.Collections.Generic;
using System.Linq;
using SIAD.Core.Constants;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using SIAD.Data.Auditoria;

namespace SIAD.Services.Auditoria;

public sealed class BitacoraMaestrosWriter : IBitacoraMaestrosWriter
{
    private readonly SiadDbContext _context;
    private readonly IAuditConfigProvider _config;
    private readonly ICurrentCompanyService _company;
    private readonly ICurrentUserAudit _user;

    public BitacoraMaestrosWriter(SiadDbContext context, IAuditConfigProvider config, ICurrentCompanyService company, ICurrentUserAudit user)
        => (_context, _config, _company, _user) = (context, config, company, user);

    public async Task RegistrarAsync(string tabla, string accion, string? registroId, string entidad, string descripcion,
                                     IReadOnlyList<AuditDiff.Campo>? campos, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (!_config.DebeAuditar(companyId, tabla, accion)) return;

        campos ??= Array.Empty<AuditDiff.Campo>();
        string? anteriores = accion == AccionesBitacora.Creacion ? null : AuditDiff.SerializeAnteriores(campos);
        string? nuevos = accion == AccionesBitacora.Eliminacion ? null : AuditDiff.SerializeNuevos(campos);

        var fila = new bitacora_maestros
        {
            company_id = companyId,
            modulo = Truncar(AuditableMaestros.All.FirstOrDefault(x => string.Equals(x.Tabla, tabla, StringComparison.OrdinalIgnoreCase))?.Modulo ?? tabla, 80),
            tabla = Truncar(tabla, 128),
            entidad = Truncar(string.IsNullOrWhiteSpace(entidad) ? tabla : entidad, 256),
            registro_id = registroId,
            accion = accion,
            descripcion = Truncar(string.IsNullOrWhiteSpace(descripcion) ? $"{accion} en {tabla}" : descripcion, 500),
            valores_anteriores = anteriores,
            valores_nuevos = nuevos,
            usuario = Truncar(string.IsNullOrWhiteSpace(_user.Usuario) ? "system" : _user.Usuario, 256),
            fecha = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
        };
        _context.bitacora_maestros.Add(fila);
        await _context.SaveChangesAsync(ct);
    }

    private static string Truncar(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);
}
