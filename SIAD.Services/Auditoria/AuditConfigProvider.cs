using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using SIAD.Core.Constants;

namespace SIAD.Services.Auditoria;

public sealed class AuditConfigProvider : IAuditConfigProvider
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    public AuditConfigProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        => (_cache, _scopeFactory) = (cache, scopeFactory);

    private sealed record Flags(bool Crear, bool Editar, bool Eliminar);

    private static string Key(long companyId) => $"auditcfg:{companyId}";

    public bool DebeAuditar(long companyId, string tabla, string accion)
    {
        if (companyId <= 0 || !AuditableMaestros.EsAuditable(tabla)) return false;
        var map = _cache.GetOrCreate(Key(companyId), e =>
        {
            e.AbsoluteExpirationRelativeToNow = System.TimeSpan.FromMinutes(30);
            return Load(companyId);
        })!;
        if (!map.TryGetValue(tabla, out var f)) return false;
        return accion switch
        {
            AccionesBitacora.Creacion => f.Crear,
            AccionesBitacora.Actualizacion => f.Editar,
            AccionesBitacora.Eliminacion => f.Eliminar,
            _ => false
        };
    }

    public void Invalidar(long companyId) => _cache.Remove(Key(companyId));

    private Dictionary<string, Flags> Load(long companyId)
    {
        using var scope = _scopeFactory.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<SIAD.Data.SiadDbContext>();
        // IgnoreQueryFilters: el provider corre fuera del scope del usuario; filtra explícito por companyId.
        return ctx.bitacora_maestro_configs
            .IgnoreQueryFilters()
            .Where(c => c.company_id == companyId && c.habilitado)
            .AsNoTracking()
            .ToDictionary(c => c.entidad, c => new Flags(c.audita_crear, c.audita_editar, c.audita_eliminar),
                System.StringComparer.OrdinalIgnoreCase);
    }
}
