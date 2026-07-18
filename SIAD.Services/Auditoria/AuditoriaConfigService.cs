using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Auditoria;

public sealed class AuditoriaConfigService : IAuditoriaConfigService
{
    private readonly SiadDbContext _context;
    private readonly IAuditConfigProvider _provider;
    private readonly ICurrentCompanyService _company;

    public AuditoriaConfigService(SiadDbContext context, IAuditConfigProvider provider, ICurrentCompanyService company)
        => (_context, _provider, _company) = (context, provider, company);

    public async Task<IReadOnlyList<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default)
    {
        var guardadas = await _context.bitacora_maestro_configs.AsNoTracking()
            .ToDictionaryAsync(c => c.entidad, c => c, System.StringComparer.OrdinalIgnoreCase, ct);

        return AuditableMaestros.All.Select(m =>
        {
            guardadas.TryGetValue(m.Tabla, out var cfg);
            return new AuditoriaConfigItemDto
            {
                Entidad = m.Tabla, Nombre = m.Nombre, Modulo = m.Modulo,
                Habilitado = cfg?.habilitado ?? false,
                AuditaCrear = cfg?.audita_crear ?? true,
                AuditaEditar = cfg?.audita_editar ?? true,
                AuditaEliminar = cfg?.audita_eliminar ?? true,
            };
        }).ToList();
    }

    public async Task GuardarAsync(IReadOnlyList<AuditoriaConfigItemDto> items, string user, CancellationToken ct = default)
    {
        var validas = items.Where(i => AuditableMaestros.EsAuditable(i.Entidad)).ToList();
        var existentes = await _context.bitacora_maestro_configs
            .ToDictionaryAsync(c => c.entidad, c => c, System.StringComparer.OrdinalIgnoreCase, ct);
        var ahora = System.DateTime.SpecifyKind(System.DateTime.UtcNow, System.DateTimeKind.Unspecified);

        foreach (var i in validas)
        {
            if (existentes.TryGetValue(i.Entidad, out var cfg))
            {
                cfg.habilitado = i.Habilitado;
                cfg.audita_crear = i.AuditaCrear; cfg.audita_editar = i.AuditaEditar; cfg.audita_eliminar = i.AuditaEliminar;
                cfg.usuariomodificacion = user; cfg.fechamodificacion = ahora;
            }
            else
            {
                _context.bitacora_maestro_configs.Add(new bitacora_maestro_config
                {
                    entidad = i.Entidad, habilitado = i.Habilitado,
                    audita_crear = i.AuditaCrear, audita_editar = i.AuditaEditar, audita_eliminar = i.AuditaEliminar,
                    usuariocreacion = user, fechacreacion = ahora
                });
            }
        }
        await _context.SaveChangesAsync(ct);
        _provider.Invalidar(_company.GetCompanyId());
    }
}
