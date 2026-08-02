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
    private readonly IAuditConfigProvider _configProvider;
    private readonly IAuditableCatalogProvider _catalogProvider;
    private readonly ICurrentCompanyService _company;

    public AuditoriaConfigService(
        SiadDbContext context,
        IAuditConfigProvider configProvider,
        IAuditableCatalogProvider catalogProvider,
        ICurrentCompanyService company)
        => (_context, _configProvider, _catalogProvider, _company) = (context, configProvider, catalogProvider, company);

    public async Task<IReadOnlyList<AuditoriaConfigItemDto>> GetAsync(CancellationToken ct = default)
    {
        await AsegurarCatalogoAsync("system", ct);

        var catalogo = await _context.bitacora_maestro_catalogos.AsNoTracking()
            .Where(c => c.activo)
            .ToListAsync(ct);

        var guardadas = await _context.bitacora_maestro_configs.AsNoTracking()
            .ToDictionaryAsync(c => c.entidad, c => c, System.StringComparer.OrdinalIgnoreCase, ct);

        return catalogo.Select(m =>
        {
            guardadas.TryGetValue(m.tabla, out var cfg);
            return new AuditoriaConfigItemDto
            {
                Entidad = m.tabla, Nombre = m.nombre, Modulo = m.modulo,
                Habilitado = cfg?.habilitado ?? false,
                AuditaCrear = cfg?.audita_crear ?? true,
                AuditaEditar = cfg?.audita_editar ?? true,
                AuditaEliminar = cfg?.audita_eliminar ?? true,
            };
        })
        .OrderBy(x => x.Modulo, System.StringComparer.OrdinalIgnoreCase)
        .ThenBy(x => x.Nombre, System.StringComparer.OrdinalIgnoreCase)
        .ToList();
    }

    public async Task GuardarAsync(IReadOnlyList<AuditoriaConfigItemDto> items, string user, CancellationToken ct = default)
    {
        var tablasCatalogo = (await _context.bitacora_maestro_catalogos.AsNoTracking()
                .Where(c => c.activo).Select(c => c.tabla).ToListAsync(ct))
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);

        var validas = items.Where(i => tablasCatalogo.Contains(i.Entidad)).ToList();
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
        _configProvider.Invalidar(_company.GetCompanyId());
    }

    public async Task<IReadOnlyList<CatalogoTablaDisponibleDto>> TablasDisponiblesAsync(CancellationToken ct = default)
    {
        var enCatalogo = (await _context.bitacora_maestro_catalogos.AsNoTracking().Select(c => c.tabla).ToListAsync(ct))
            .ToHashSet(System.StringComparer.OrdinalIgnoreCase);
        var vistas = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
        var result = new Dictionary<string, CatalogoTablaDisponibleDto>(System.StringComparer.OrdinalIgnoreCase);
        foreach (var et in _context.Model.GetEntityTypes())
        {
            var tabla = et.GetTableName();
            if (string.IsNullOrEmpty(tabla)) continue;
            if (et.GetViewName() is not null) { vistas.Add(tabla); continue; }   // es vista
            if (et.FindPrimaryKey() is null) continue;                            // keyless → no auditable por interceptor
            if (tabla.StartsWith("bitacora_", System.StringComparison.OrdinalIgnoreCase)) continue; // tablas de la propia bitácora
            if (enCatalogo.Contains(tabla)) continue;                            // ya está en el catálogo
            result.TryAdd(tabla, new CatalogoTablaDisponibleDto { Tabla = tabla, ModuloSugerido = ModuloPorPrefijo(tabla) });
        }
        foreach (var v in vistas) result.Remove(v); // por si una vista comparte nombre
        return result.Values.OrderBy(x => x.Tabla, System.StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task AgregarAlCatalogoAsync(AgregarMaestroDto dto, string user, CancellationToken ct = default)
    {
        var esAuditable = _context.Model.GetEntityTypes().Any(e =>
            string.Equals(e.GetTableName(), dto.Tabla, System.StringComparison.OrdinalIgnoreCase)
            && e.FindPrimaryKey() != null
            && e.GetViewName() == null);
        if (!esAuditable)
            throw new System.InvalidOperationException(
                $"La tabla '{dto.Tabla}' no existe o no es auditable (debe tener clave y no ser vista).");

        var yaExiste = await _context.bitacora_maestro_catalogos
            .AnyAsync(c => c.tabla.ToLower() == dto.Tabla.ToLower(), ct);
        if (yaExiste)
            throw new System.InvalidOperationException($"La tabla '{dto.Tabla}' ya está en el catálogo.");

        var ahora = System.DateTime.SpecifyKind(System.DateTime.UtcNow, System.DateTimeKind.Unspecified);
        _context.bitacora_maestro_catalogos.Add(new bitacora_maestro_catalogo
        {
            tabla = dto.Tabla, nombre = dto.Nombre, modulo = dto.Modulo, activo = true,
            usuariocreacion = string.IsNullOrWhiteSpace(user) ? "system" : user, fechacreacion = ahora
        });
        await _context.SaveChangesAsync(ct);
        _catalogProvider.Invalidar(_company.GetCompanyId());
    }

    public async Task DesactivarCatalogoAsync(string tabla, string user, CancellationToken ct = default)
    {
        var fila = await _context.bitacora_maestro_catalogos
            .FirstOrDefaultAsync(c => c.tabla.ToLower() == tabla.ToLower(), ct);
        if (fila is null) return;

        var ahora = System.DateTime.SpecifyKind(System.DateTime.UtcNow, System.DateTimeKind.Unspecified);
        fila.activo = false;
        fila.usuariomodificacion = user;
        fila.fechamodificacion = ahora;
        await _context.SaveChangesAsync(ct);
        _catalogProvider.Invalidar(_company.GetCompanyId());
    }

    private async Task AsegurarCatalogoAsync(string user, CancellationToken ct)
    {
        if (await _context.bitacora_maestro_catalogos.AnyAsync(ct)) return;
        var ahora = System.DateTime.SpecifyKind(System.DateTime.UtcNow, System.DateTimeKind.Unspecified);
        foreach (var m in AuditableMaestros.All)
        {
            _context.bitacora_maestro_catalogos.Add(new bitacora_maestro_catalogo
            {
                tabla = m.Tabla, nombre = m.Nombre, modulo = m.Modulo, activo = true,
                usuariocreacion = string.IsNullOrWhiteSpace(user) ? "system" : user, fechacreacion = ahora
            });
        }
        await _context.SaveChangesAsync(ct);
    }

    private static string ModuloPorPrefijo(string tabla)
    {
        if (tabla.StartsWith("ban_", System.StringComparison.OrdinalIgnoreCase)
            || tabla.StartsWith("bnc_", System.StringComparison.OrdinalIgnoreCase)) return "Bancos";
        if (tabla.StartsWith("alm_", System.StringComparison.OrdinalIgnoreCase)) return "Almacén";
        if (tabla.StartsWith("cln_", System.StringComparison.OrdinalIgnoreCase)
            || tabla.StartsWith("cliente", System.StringComparison.OrdinalIgnoreCase)) return "Clientes";
        if (tabla.StartsWith("prv_", System.StringComparison.OrdinalIgnoreCase)) return "Proveedores";
        if (tabla.StartsWith("con_", System.StringComparison.OrdinalIgnoreCase)
            || tabla.StartsWith("cnt_", System.StringComparison.OrdinalIgnoreCase)) return "Contabilidad";
        if (tabla.StartsWith("pst_", System.StringComparison.OrdinalIgnoreCase)) return "Presupuesto";
        return string.Empty;
    }
}
