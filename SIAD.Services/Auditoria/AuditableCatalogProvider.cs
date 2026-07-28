using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;

namespace SIAD.Services.Auditoria;

public sealed class AuditableCatalogProvider : IAuditableCatalogProvider
{
    private readonly IMemoryCache _cache;
    private readonly IServiceScopeFactory _scopeFactory;
    public AuditableCatalogProvider(IMemoryCache cache, IServiceScopeFactory scopeFactory)
        => (_cache, _scopeFactory) = (cache, scopeFactory);

    private sealed record Item(string Nombre, string Modulo);
    private static string Key(long companyId) => $"auditcat:{companyId}";

    private Dictionary<string, Item> Mapa(long companyId)
        => _cache.GetOrCreate(Key(companyId), e =>
        {
            e.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30);
            using var scope = _scopeFactory.CreateScope();
            var ctx = scope.ServiceProvider.GetRequiredService<SIAD.Data.SiadDbContext>();
            return ctx.bitacora_maestro_catalogos
                .IgnoreQueryFilters()
                .Where(c => c.company_id == companyId && c.activo)
                .AsNoTracking()
                .ToDictionary(c => c.tabla, c => new Item(c.nombre, c.modulo), StringComparer.OrdinalIgnoreCase);
        })!;

    public bool EsAuditable(long companyId, string tabla)
        => companyId > 0 && Mapa(companyId).ContainsKey(tabla);

    public string NombreDe(long companyId, string tabla)
        => Mapa(companyId).TryGetValue(tabla, out var i) ? i.Nombre : tabla;

    public string ModuloDe(long companyId, string tabla)
        => Mapa(companyId).TryGetValue(tabla, out var i) ? i.Modulo : tabla;

    public void Invalidar(long companyId) => _cache.Remove(Key(companyId));
}
