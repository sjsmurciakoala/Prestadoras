using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Reporte de existencias por bodega (valorado). Lee la fuente de verdad del stock por bodega
/// (<c>alm_articulo_bodega</c>), acota por bodega/tipo/estado/búsqueda y valoriza cada línea al
/// costo promedio de su bodega. El agrupado por bodega y los subtotales los hace la vista (grid
/// o reporte); aquí sólo se devuelve la lista plana ordenada por bodega y artículo.
/// </summary>
public sealed class ExistenciasBodegaService : IExistenciasBodegaService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public ExistenciasBodegaService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    public async Task<IReadOnlyList<ExistenciaBodegaItemDto>> GetAsync(ExistenciasBodegaFilterDto filtro, CancellationToken ct = default)
    {
        filtro ??= new ExistenciasBodegaFilterDto();

        // Universo: ubicaciones ACTIVAS de artículos ACTIVOS (el rollup del maestro usa el mismo
        // contrato: sólo filas activas). company_id lo aplica el filtro global de tenant.
        var query = _context.alm_articulo_bodegas
            .AsNoTracking()
            .Where(u => u.activo)
            .Where(u => u.articulo != null && u.articulo.activo);

        if (filtro.BodegaId.HasValue)
        {
            query = query.Where(u => u.bodega_id == filtro.BodegaId.Value);
        }

        if (filtro.TipoArticuloId.HasValue)
        {
            query = query.Where(u => u.articulo!.tipo_articulo_id == filtro.TipoArticuloId.Value);
        }

        if (!filtro.IncluirSinExistencia)
        {
            // Un reporte de existencias muestra lo que HAY: se omiten las líneas en cero
            // (la ubicación existe pero sin stock). El switch de la UI las trae de vuelta.
            query = query.Where(u => u.existencia != 0);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(u => u.articulo != null && (
                    EF.Functions.ILike(u.articulo.codigo_articulo ?? string.Empty, likePattern)
                    || EF.Functions.ILike(u.articulo.descripcion, likePattern)));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(u => u.articulo != null && (
                    (u.articulo.codigo_articulo ?? string.Empty).ToLowerInvariant().Contains(lowered)
                    || u.articulo.descripcion.ToLowerInvariant().Contains(lowered)));
            }
        }

        return await query
            .OrderBy(u => u.bodega!.codigo)
            .ThenBy(u => u.articulo!.descripcion)
            .Select(u => new ExistenciaBodegaItemDto
            {
                Id = u.id,
                BodegaId = u.bodega_id,
                BodegaCodigo = u.bodega != null ? u.bodega.codigo : string.Empty,
                BodegaNombre = u.bodega != null ? u.bodega.nombre : string.Empty,
                ArticuloId = u.articulo_id,
                ArticuloCodigo = u.articulo != null ? u.articulo.codigo_articulo : null,
                ArticuloDescripcion = u.articulo != null ? u.articulo.descripcion : string.Empty,
                UnidadMedida = u.articulo != null && u.articulo.unidad_medida_ref != null
                    ? (u.articulo.unidad_medida_ref.abreviatura ?? u.articulo.unidad_medida_ref.codigo)
                    : null,
                TipoArticulo = u.articulo != null && u.articulo.tipo_articulo_ref != null
                    ? u.articulo.tipo_articulo_ref.nombre : null,
                Existencia = u.existencia,
                ExistenciaMinima = u.existencia_minima,
                CostoPromedio = u.costo_promedio,
                // Valor a costo promedio de la bodega (la valuación real de lo que hay físicamente ahí).
                Valor = u.existencia * u.costo_promedio
            })
            .ToListAsync(ct);
    }

    public async Task<ExistenciasBodegaImpresionDto> GetDatosImpresionAsync(
        ExistenciasBodegaFilterDto filtro, string impresoPor, CancellationToken ct = default)
    {
        filtro ??= new ExistenciasBodegaFilterDto();

        var items = (await GetAsync(filtro, ct)).ToList();
        var empresa = await CargarEmpresaAsync(ct);

        // Texto de filtros para el encabezado. Los nombres de bodega/tipo se toman de la primera
        // línea (si se filtró, todas comparten el mismo) para no hacer lookups extra.
        var bodegaTexto = filtro.BodegaId.HasValue
            ? (items.FirstOrDefault()?.BodegaDisplay ?? "Bodega seleccionada")
            : "Todas las bodegas";
        var tipoTexto = filtro.TipoArticuloId.HasValue
            ? (items.FirstOrDefault()?.TipoArticulo ?? "Tipo seleccionado")
            : "Todos los tipos";
        var alcanceTexto = filtro.IncluirSinExistencia ? "incluye sin existencia" : "solo con existencia";

        return new ExistenciasBodegaImpresionDto
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = string.IsNullOrWhiteSpace(impresoPor) ? "sistema" : impresoPor.Trim(),
            Items = items,
            FiltroTexto = $"{bodegaTexto} · {tipoTexto} · {alcanceTexto}"
        };
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }
}
