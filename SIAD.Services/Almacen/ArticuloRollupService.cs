using Microsoft.EntityFrameworkCore;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Rollup de cabecera del artículo. Ver <see cref="IArticuloRollupService"/>.
/// <para>
/// <b>Se escribe con <c>ExecuteUpdateAsync</c>, deliberadamente fuera de
/// <c>SaveChanges</c>.</b> Dos razones, ambas verificadas en el código:
/// </para>
/// <list type="number">
///   <item><b>Bitácora.</b> <c>alm_articulo</c> está en la lista blanca de auditoría
///   (<c>AuditableMaestros</c>) y el interceptor captura las entidades modificadas en
///   <c>SavingChanges</c>. Un lote de miles de pares generaría miles de filas de auditoría
///   por algo que no es una edición de maestro, sino un efecto de posteo.</item>
///   <item><b>Concurrencia.</b> <c>alm_articulo</c> lleva token <c>xmin</c>: un UPDATE por
///   <c>SaveChanges</c> durante el lote chocaría con cualquier usuario que guarde ese
///   artículo en paralelo y abortaría con <c>DbUpdateConcurrencyException</c>.</item>
/// </list>
/// <para>
/// <c>ExecuteUpdateAsync</c> es una consulta LINQ, así que el filtro global por
/// <c>company_id</c> SÍ se aplica: la actualización no puede cruzar de empresa.
/// </para>
/// </summary>
public sealed class ArticuloRollupService : IArticuloRollupService
{
    private readonly SiadDbContext _context;

    public ArticuloRollupService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task RecomputeAsync(int articuloId, CancellationToken ct = default)
    {
        if (articuloId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(articuloId), "El artículo no es válido.");
        }

        // Σ de las ubicaciones ACTIVAS. Sin filas activas el rollup queda en 0, que es lo
        // correcto: la existencia de una bodega deshabilitada no cuenta para el total.
        var totales = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId && u.activo)
            .GroupBy(u => u.articulo_id)
            .Select(g => new
            {
                Existencia = g.Sum(x => x.existencia),
                Minima = g.Sum(x => x.existencia_minima)
            })
            .FirstOrDefaultAsync(ct);

        var existencia = totales?.Existencia ?? 0m;
        var minima = totales?.Minima ?? 0m;

        await _context.alm_articulos
            .Where(a => a.id == articuloId)
            .ExecuteUpdateAsync(s => s
                .SetProperty(a => a.existencia, existencia)
                .SetProperty(a => a.existencia_minima, minima)
                // cantidad es la columna legacy de SIMAFI: se mantiene igual a existencia.
                .SetProperty(a => a.cantidad, existencia), ct);
    }
}
