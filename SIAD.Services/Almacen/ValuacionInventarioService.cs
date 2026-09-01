using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Valuación de inventario a una fecha de corte. Reconstruye el saldo por (artículo, bodega)
/// leyendo el ÚLTIMO asiento del kardex con fecha ≤ corte que tenga snapshot materializado
/// (<c>existencia_resultante</c> / <c>costo_promedio_resultante</c>), que es lo que escribe el
/// motor de posteo. El histórico SIMAFI (resultante NULL) queda fuera: la valuación a fecha
/// parte de la carga inicial del inventario. Produce las mismas columnas que el reporte de
/// existencias (<see cref="ExistenciaBodegaItemDto"/>), así que comparte pantalla y PDF.
/// </summary>
public sealed class ValuacionInventarioService : IValuacionInventarioService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public ValuacionInventarioService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    public async Task<IReadOnlyList<ExistenciaBodegaItemDto>> GetAsync(ValuacionInventarioFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new ValuacionInventarioFilterDto();
        var fecha = filtro.FechaCorte ?? DateOnly.FromDateTime(DateTime.Today);

        // Sólo asientos posteados por el motor (con snapshot) hasta la fecha. El histórico SIMAFI
        // tiene existencia_resultante NULL y queda fuera; además así se traen POCAS filas (no los
        // ~decenas de miles de asientos migrados), lo que hace viable el corte por par en memoria.
        var query = _context.alm_kardexs.AsNoTracking()
            .Where(k => k.fecha != null && k.fecha <= fecha
                     && k.existencia_resultante != null
                     && k.articulo_id != null && k.bodega_id != null);

        if (filtro.BodegaId.HasValue)
        {
            query = query.Where(k => k.bodega_id == filtro.BodegaId.Value);
        }

        if (filtro.TipoArticuloId.HasValue)
        {
            query = query.Where(k => k.articulo_ref != null && k.articulo_ref.tipo_articulo_id == filtro.TipoArticuloId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(k => k.articulo_ref != null && (
                    EF.Functions.ILike(k.articulo_ref.codigo_articulo ?? string.Empty, likePattern)
                    || EF.Functions.ILike(k.articulo_ref.descripcion, likePattern)));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(k => k.articulo_ref != null && (
                    (k.articulo_ref.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered)
                    || k.articulo_ref.descripcion.ToLowerInvariant().Contains(lowered)));
            }
        }

        var asientos = await query
            .Select(k => new
            {
                ArticuloId = k.articulo_id!.Value,
                BodegaId = k.bodega_id!.Value,
                Fecha = k.fecha!.Value,
                k.id,
                Existencia = k.existencia_resultante!.Value,
                Costo = k.costo_promedio_resultante ?? 0m
            })
            .ToListAsync(ct);

        if (asientos.Count == 0)
        {
            return new List<ExistenciaBodegaItemDto>();
        }

        // Corte por par (artículo, bodega): el último asiento cronológico (fecha, id) ≤ corte es el
        // saldo vigente a esa fecha. Se descartan los que quedaron en cero (no había existencia).
        var ultimos = asientos
            .GroupBy(a => (a.ArticuloId, a.BodegaId))
            .Select(g => g.OrderByDescending(x => x.Fecha).ThenByDescending(x => x.id).First())
            .Where(x => x.Existencia != 0m)
            .ToList();

        if (ultimos.Count == 0)
        {
            return new List<ExistenciaBodegaItemDto>();
        }

        // Nombres de artículo y bodega para los ids presentes (dos consultas, no N+1).
        var articuloIds = ultimos.Select(x => x.ArticuloId).Distinct().ToList();
        var bodegaIds = ultimos.Select(x => x.BodegaId).Distinct().ToList();

        var articulos = (await _context.alm_articulos.AsNoTracking()
            .Where(a => articuloIds.Contains(a.id))
            .Select(a => new
            {
                a.id,
                a.codigo_articulo,
                a.descripcion,
                Tipo = a.tipo_articulo_ref != null ? a.tipo_articulo_ref.nombre : null,
                Unidad = a.unidad_medida_ref != null ? (a.unidad_medida_ref.abreviatura ?? a.unidad_medida_ref.codigo) : null
            })
            .ToListAsync(ct)).ToDictionary(a => a.id);

        var bodegas = (await _context.alm_bodegas.AsNoTracking()
            .Where(b => bodegaIds.Contains(b.id))
            .Select(b => new { b.id, b.codigo, b.nombre })
            .ToListAsync(ct)).ToDictionary(b => b.id);

        return ultimos
            .OrderBy(x => bodegas.TryGetValue(x.BodegaId, out var b) ? b.codigo : string.Empty)
            .ThenBy(x => articulos.TryGetValue(x.ArticuloId, out var a) ? a.descripcion : string.Empty)
            .Select((x, idx) =>
            {
                articulos.TryGetValue(x.ArticuloId, out var a);
                bodegas.TryGetValue(x.BodegaId, out var b);
                return new ExistenciaBodegaItemDto
                {
                    Id = idx,
                    BodegaId = x.BodegaId,
                    BodegaCodigo = b?.codigo ?? string.Empty,
                    BodegaNombre = b?.nombre ?? string.Empty,
                    ArticuloId = x.ArticuloId,
                    ArticuloCodigo = a?.codigo_articulo,
                    ArticuloDescripcion = a?.descripcion ?? string.Empty,
                    UnidadMedida = a?.Unidad,
                    TipoArticulo = a?.Tipo,
                    Existencia = x.Existencia,
                    ExistenciaMinima = 0m,
                    CostoPromedio = x.Costo,
                    Valor = x.Existencia * x.Costo
                };
            })
            .ToList();
    }

    public async Task<ExistenciasBodegaImpresionDto> GetDatosImpresionAsync(
        ValuacionInventarioFilterDto filtro, string impresoPor, CancellationToken ct = default)
    {
        filtro ??= new ValuacionInventarioFilterDto();
        var fecha = filtro.FechaCorte ?? DateOnly.FromDateTime(DateTime.Today);

        var items = (await GetAsync(filtro, ct)).ToList();
        var empresa = await CargarEmpresaAsync(ct);

        var bodegaTexto = filtro.BodegaId.HasValue
            ? (items.FirstOrDefault()?.BodegaDisplay ?? "Bodega seleccionada")
            : "Todas las bodegas";
        var tipoTexto = filtro.TipoArticuloId.HasValue
            ? (items.FirstOrDefault()?.TipoArticulo ?? "Tipo seleccionado")
            : "Todos los tipos";

        return new ExistenciasBodegaImpresionDto
        {
            Titulo = $"VALUACIÓN DE INVENTARIO AL {fecha:dd/MM/yyyy}",
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),
            Items = items,
            FiltroTexto = $"Corte al {fecha:dd/MM/yyyy} · {bodegaTexto} · {tipoTexto}"
        };
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }
}
