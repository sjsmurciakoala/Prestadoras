using Microsoft.EntityFrameworkCore;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Resuelve la tasa de ISV de compras por artículo (capa 1). Antes esta regla vivía duplicada
/// dentro de <c>RecepcionCompraService</c>; se extrajo aquí para tener una sola definición: el
/// tipo del artículo (<c>alm_tipo_articulo.impuesto_tasa_id</c>) apunta a una tasa del catálogo
/// global <c>cfg_impuesto_tasa</c>, y se toma la que regía a la FECHA del documento (no la de
/// hoy) para que recalcular o reimprimir un documento viejo dé el impuesto de su época.
/// <para>
/// Multiempresa: <c>alm_articulo</c> y <c>alm_tipo_articulo</c> llevan el filtro global de
/// <c>company_id</c>, así que la resolución queda acotada al tenant actual sin código extra.
/// </para>
/// </summary>
public sealed class TasaIsvArticuloResolver : ITasaIsvArticuloResolver
{
    private readonly SiadDbContext _context;

    public TasaIsvArticuloResolver(SiadDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<int, TasaIsvArticulo>> ResolverAsync(
        IReadOnlyCollection<int> articuloIds, DateOnly fecha, CancellationToken ct = default)
    {
        var ids = articuloIds?.Where(id => id > 0).Distinct().ToList() ?? new List<int>();
        if (ids.Count == 0) return new Dictionary<int, TasaIsvArticulo>();

        // Artículo → (tasa asignada al tipo, nombre del tipo). LEFT JOIN al tipo para incluir
        // también los artículos SIN tipo: deben salir en el resultado como "no configurado",
        // no desaparecer (así el llamador puede avisar por ese renglón).
        var infos = await (
            from a in _context.alm_articulos.AsNoTracking()
            where ids.Contains(a.id)
            join t in _context.alm_tipo_articulos.AsNoTracking() on a.tipo_articulo_id equals t.id into tj
            from t in tj.DefaultIfEmpty()
            select new
            {
                ArticuloId = a.id,
                TasaId = t != null ? t.impuesto_tasa_id : null,
                TipoNombre = t != null ? t.nombre : null
            }).ToListAsync(ct);

        var tasaIds = infos
            .Where(x => x.TasaId != null)
            .Select(x => x.TasaId!.Value)
            .Distinct()
            .ToList();

        // Vigencia a la fecha del documento: la tasa que regía entonces, no la de hoy.
        var porTasa = tasaIds.Count == 0
            ? new Dictionary<int, decimal>()
            : (await _context.cfg_impuesto_tasas.AsNoTracking()
                    .Where(t => tasaIds.Contains(t.id)
                             && t.vigencia_desde <= fecha
                             && (t.vigencia_hasta == null || t.vigencia_hasta >= fecha))
                    .Select(t => new { t.id, t.porcentaje })
                    .ToListAsync(ct))
                .ToDictionary(t => t.id, t => t.porcentaje);

        var resultado = new Dictionary<int, TasaIsvArticulo>(ids.Count);
        foreach (var x in infos)
        {
            var porcentaje = x.TasaId != null && porTasa.TryGetValue(x.TasaId.Value, out var p) ? p : 0m;
            resultado[x.ArticuloId] = new TasaIsvArticulo(porcentaje, x.TasaId != null, x.TipoNombre);
        }

        return resultado;
    }
}
