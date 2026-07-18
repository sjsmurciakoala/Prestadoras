using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SIAD.Core.Constants;
using SIAD.Core.Entities;
using SIAD.Data.Auditoria;

namespace SIAD.Services.Auditoria;

public sealed class BitacoraMaestrosInterceptor : SaveChangesInterceptor
{
    private readonly IAuditConfigProvider _config;
    private readonly ICurrentUserAudit _user;
    private readonly List<PendienteAdd> _pendientesAdd = new();
    private bool _reentrada;

    public BitacoraMaestrosInterceptor(IAuditConfigProvider config, ICurrentUserAudit user)
        => (_config, _user) = (config, user);

    private sealed record PendienteAdd(EntityEntry Entry, string Tabla, long CompanyId);

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r)
    { Capturar(e.Context); return base.SavingChanges(e, r); }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { Capturar(e.Context); return base.SavingChangesAsync(e, r, ct); }

    public override int SavedChanges(SaveChangesCompletedEventData e, int result)
    { CompletarAsync(e.Context, CancellationToken.None).AsTask().GetAwaiter().GetResult(); return base.SavedChanges(e, result); }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData e, int result, CancellationToken ct = default)
    { await CompletarAsync(e.Context, ct); return await base.SavedChangesAsync(e, result, ct); }

    private void Capturar(DbContext? ctx)
    {
        if (ctx is null || _reentrada) return;
        _pendientesAdd.Clear();
        var nuevas = new List<bitacora_maestros>();

        foreach (var entry in ctx.ChangeTracker.Entries().ToList())
        {
            var tabla = entry.Metadata.GetTableName();
            if (tabla is null) continue;
            if (tabla is "bitacora_maestros" or "bitacora_maestro_config") continue;
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;

            var accion = ResolverAccion(entry);
            var companyId = LeerCompanyId(entry);
            if (!_config.DebeAuditar(companyId, tabla, accion)) continue;

            if (entry.State == EntityState.Added)
            {
                _pendientesAdd.Add(new PendienteAdd(entry, tabla, companyId));
                continue;
            }

            var campos = entry.State == EntityState.Deleted ? CamposEliminacion(entry) : CamposModificacion(entry);
            var anteriores = AuditDiff.SerializeAnteriores(campos);
            var nuevos = entry.State == EntityState.Deleted ? null : AuditDiff.SerializeNuevos(campos);
            nuevas.Add(Construir(companyId, tabla, entry, accion, campos.Count, anteriores, nuevos, LeerPk(entry)));
        }

        if (nuevas.Count > 0) ctx.Set<bitacora_maestros>().AddRange(nuevas);
    }

    private async ValueTask CompletarAsync(DbContext? ctx, CancellationToken ct)
    {
        if (ctx is null || _reentrada || _pendientesAdd.Count == 0) return;
        var pend = _pendientesAdd.ToList();
        _pendientesAdd.Clear();

        var filas = new List<bitacora_maestros>();
        foreach (var p in pend)
        {
            var campos = CamposCreacion(p.Entry); // la PK ya está generada
            var nuevos = AuditDiff.SerializeNuevos(campos);
            filas.Add(Construir(p.CompanyId, p.Tabla, p.Entry, AccionesBitacora.Creacion, campos.Count, null, nuevos, LeerPk(p.Entry)));
        }

        try
        {
            _reentrada = true;
            ctx.Set<bitacora_maestros>().AddRange(filas);
            await ctx.SaveChangesAsync(ct);
        }
        finally { _reentrada = false; }
    }

    private bitacora_maestros Construir(long companyId, string tabla, EntityEntry entry, string accion, int nCampos, string? anteriores, string? nuevos, string? registroId)
        => new()
        {
            company_id = companyId,
            modulo = Truncar(AuditableMaestros.All.FirstOrDefault(x => string.Equals(x.Tabla, tabla, StringComparison.OrdinalIgnoreCase))?.Modulo ?? tabla, 80),
            tabla = Truncar(tabla, 128),
            entidad = Truncar(Descriptor(entry, tabla), 256),
            registro_id = registroId,
            accion = accion,
            descripcion = Truncar(Descripcion(accion, tabla, nCampos), 500),
            valores_anteriores = anteriores,
            valores_nuevos = nuevos,
            usuario = Truncar(string.IsNullOrWhiteSpace(_user.Usuario) ? "system" : _user.Usuario, 256),
            fecha = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified),
        };

    private static string ResolverAccion(EntityEntry entry)
    {
        if (entry.State == EntityState.Added) return AccionesBitacora.Creacion;
        if (entry.State == EntityState.Deleted) return AccionesBitacora.Eliminacion;
        var activo = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "activo");
        if (activo is not null && activo.IsModified && activo.OriginalValue is true && activo.CurrentValue is false)
            return AccionesBitacora.Eliminacion;
        return AccionesBitacora.Actualizacion;
    }

    private static long LeerCompanyId(EntityEntry entry)
    {
        var p = entry.Properties.FirstOrDefault(x => x.Metadata.Name == "company_id");
        return p?.CurrentValue switch { long l => l, int i => i, _ => 0 };
    }

    private static List<AuditDiff.Campo> CamposCreacion(EntityEntry entry)
    {
        var r = new List<AuditDiff.Campo>();
        foreach (var p in entry.Properties)
        {
            if (p.CurrentValue is byte[]) continue;
            r.Add(new(p.Metadata.Name, null, p.CurrentValue));
        }
        return r;
    }

    private static List<AuditDiff.Campo> CamposEliminacion(EntityEntry entry)
    {
        var r = new List<AuditDiff.Campo>();
        foreach (var p in entry.Properties)
        {
            if (p.OriginalValue is byte[]) continue;
            r.Add(new(p.Metadata.Name, p.OriginalValue, null));
        }
        return r;
    }

    private static List<AuditDiff.Campo> CamposModificacion(EntityEntry entry)
    {
        var r = new List<AuditDiff.Campo>();
        foreach (var p in entry.Properties)
        {
            if (!p.IsModified) continue;
            if (Equals(p.OriginalValue, p.CurrentValue)) continue;
            if (p.OriginalValue is byte[] || p.CurrentValue is byte[]) continue;
            r.Add(new(p.Metadata.Name, p.OriginalValue, p.CurrentValue));
        }
        return r;
    }

    private static string? LeerPk(EntityEntry entry)
    {
        var pk = entry.Metadata.FindPrimaryKey();
        if (pk is null) return null;
        var vals = pk.Properties.Select(p => entry.Property(p.Name).CurrentValue?.ToString()).ToArray();
        if (vals.Any(v => string.IsNullOrEmpty(v) || v == "0")) return null;
        return string.Join("|", vals);
    }

    private static string Descriptor(EntityEntry entry, string tabla)
    {
        var codigo = ValorDe(entry, "codigocliente", "cod_proveedor", "codigo", "codigo_articulo", "codigo_bodega");
        var nombre = ValorDe(entry, "nombre", "razon_social", "descripcion");
        if (!string.IsNullOrWhiteSpace(codigo) && !string.IsNullOrWhiteSpace(nombre)) return $"{codigo} - {nombre}";
        if (!string.IsNullOrWhiteSpace(nombre)) return nombre!;
        if (!string.IsNullOrWhiteSpace(codigo)) return codigo!;
        return entry.Entity.GetType().FullName ?? tabla;
    }

    private static string? ValorDe(EntityEntry entry, params string[] nombres)
    {
        foreach (var n in nombres)
        {
            var p = entry.Properties.FirstOrDefault(x => x.Metadata.Name == n);
            var v = p?.CurrentValue ?? p?.OriginalValue;
            if (v is string s && !string.IsNullOrWhiteSpace(s)) return s;
        }
        return null;
    }

    private static string Descripcion(string accion, string tabla, int nCampos)
    {
        if (accion == AccionesBitacora.Creacion) return $"Creación en {tabla}";
        if (accion == AccionesBitacora.Eliminacion) return $"Eliminación en {tabla}";
        return $"Actualización en {tabla} ({nCampos} campo(s))";
    }

    private static string Truncar(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length > max ? s[..max] : s);
}
