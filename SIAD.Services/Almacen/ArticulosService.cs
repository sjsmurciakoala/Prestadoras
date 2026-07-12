using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Catálogo de artículos de almacén (alm_articulo). Multiempresa: el filtro
/// global por company_id y el estampado en inserts los aplica SiadDbContext.
/// </summary>
public sealed class ArticulosService : IArticulosService
{
    private readonly SiadDbContext _context;

    public ArticulosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ArticuloListItemDto>> GetAsync(ArticuloFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new ArticuloFilterDto();

        var query = _context.alm_articulos.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Linea))
        {
            var linea = filtro.Linea.Trim();
            query = query.Where(a => a.linea == linea);
        }

        if (filtro.SoloBajoMinimo == true)
        {
            query = query.Where(a => a.existencia_minima > 0 && a.existencia < a.existencia_minima);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(a =>
                    EF.Functions.ILike(a.codigo_articulo, likePattern) ||
                    EF.Functions.ILike(a.descripcion, likePattern) ||
                    EF.Functions.ILike(a.diametro ?? string.Empty, likePattern));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(a =>
                    a.codigo_articulo.ToLowerInvariant().Contains(lowered) ||
                    a.descripcion.ToLowerInvariant().Contains(lowered) ||
                    (a.diametro ?? string.Empty).ToLowerInvariant().Contains(lowered));
            }
        }

        return await query
            .OrderBy(a => a.codigo_articulo)
            .Select(a => new ArticuloListItemDto
            {
                Id = a.id,
                Codigo = a.codigo_articulo,
                Descripcion = a.descripcion,
                UnidadMedida = a.unidad_medida,
                UnidadMedidaId = a.unidad_medida_id,
                UnidadMedidaCodigo = a.unidad_medida_ref != null ? a.unidad_medida_ref.codigo : null,
                Linea = a.linea,
                Grupo = a.grupo,
                TipoArticuloNombre = a.tipo_articulo_ref != null ? a.tipo_articulo_ref.nombre : null,
                LineaNombre = a.linea_ref != null ? a.linea_ref.nombre : null,
                GrupoNombre = a.grupo_ref != null ? a.grupo_ref.nombre : null,
                Diametro = a.diametro,
                CuentaContable = a.cuenta_contable,
                Existencia = a.existencia,
                ExistenciaMinima = a.existencia_minima,
                ValorUnitario = a.valor_unitario
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new AlertaStockFilterDto();

        // La existencia y el mínimo son POR BODEGA: una alerta es una fila
        // (artículo, bodega) sin stock (o negativa) o por debajo de su mínimo.
        var query = _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.existencia <= 0
                     || (u.existencia_minima > 0 && u.existencia < u.existencia_minima));

        if (filtro.SoloConMinimo == true)
        {
            query = query.Where(u => u.existencia_minima > 0);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Linea))
        {
            var linea = filtro.Linea.Trim();
            query = query.Where(u => u.articulo != null && u.articulo.linea == linea);
        }

        // El filtro de severidad se traduce a predicados sobre existencia.
        switch (filtro.Severidad)
        {
            case StockSeveridad.Negativa:
                query = query.Where(u => u.existencia < 0);
                break;
            case StockSeveridad.SinStock:
                query = query.Where(u => u.existencia == 0);
                break;
            case StockSeveridad.BajoMinimo:
                query = query.Where(u => u.existencia > 0 && u.existencia_minima > 0 && u.existencia < u.existencia_minima);
                break;
        }

        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var term = filtro.Search.Trim();
            var likePattern = $"%{term}%";

            if (_context.Database.IsRelational())
            {
                query = query.Where(u => u.articulo != null && (
                    EF.Functions.ILike(u.articulo.codigo_articulo, likePattern) ||
                    EF.Functions.ILike(u.articulo.descripcion, likePattern)));
            }
            else
            {
                var lowered = term.ToLowerInvariant();
                query = query.Where(u => u.articulo != null && (
                    u.articulo.codigo_articulo.ToLowerInvariant().Contains(lowered) ||
                    u.articulo.descripcion.ToLowerInvariant().Contains(lowered)));
            }
        }

        var filas = await query
            .Select(u => new
            {
                ArticuloId = u.articulo_id,
                Codigo = u.articulo != null ? u.articulo.codigo_articulo : string.Empty,
                Descripcion = u.articulo != null ? u.articulo.descripcion : string.Empty,
                UnidadMedida = u.articulo != null ? u.articulo.unidad_medida : null,
                Linea = u.articulo != null ? u.articulo.linea : null,
                BodegaId = u.bodega_id,
                BodegaNombre = u.bodega != null ? u.bodega.nombre : null,
                u.existencia,
                u.existencia_minima,
                ValorUnitario = u.articulo != null ? u.articulo.valor_unitario : 0m
            })
            .ToListAsync(ct);

        var alertas = filas.Select(a =>
        {
            var severidad = a.existencia < 0
                ? StockSeveridad.Negativa
                : a.existencia == 0
                    ? StockSeveridad.SinStock
                    : StockSeveridad.BajoMinimo;

            var sugerida = a.existencia_minima > 0
                ? Math.Max(0, a.existencia_minima - a.existencia)
                : 0m;

            return new AlertaStockDto
            {
                Id = a.ArticuloId,
                Codigo = a.Codigo,
                Descripcion = a.Descripcion,
                UnidadMedida = a.UnidadMedida,
                Linea = a.Linea,
                BodegaId = a.BodegaId,
                BodegaNombre = a.BodegaNombre,
                Existencia = a.existencia,
                ExistenciaMinima = a.existencia_minima,
                ValorUnitario = a.ValorUnitario,
                Severidad = severidad,
                CantidadSugerida = sugerida
            };
        });

        // Más urgente primero (negativa → sin stock → bajo mínimo), luego por
        // mayor costo de reposición.
        return alertas
            .OrderBy(a => SeveridadRank(a.Severidad))
            .ThenByDescending(a => a.ValorReposicion)
            .ThenBy(a => a.Codigo)
            .ToList();
    }

    private async Task ValidarUnidadMedidaAsync(int? unidadMedidaId, CancellationToken ct)
    {
        if (!unidadMedidaId.HasValue)
        {
            return;
        }

        var existe = await _context.alm_unidad_medidas
            .AsNoTracking()
            .AnyAsync(u => u.id == unidadMedidaId.Value && u.activo, ct);

        if (!existe)
        {
            throw new InvalidOperationException("La unidad de medida seleccionada no existe o está inactiva.");
        }
    }

    private static int SeveridadRank(string severidad) => severidad switch
    {
        StockSeveridad.Negativa => 0,
        StockSeveridad.SinStock => 1,
        StockSeveridad.BajoMinimo => 2,
        _ => 3
    };

    public async Task<ArticuloEditDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.alm_articulos
            .AsNoTracking()
            .Where(a => a.id == id)
            .Select(a => new ArticuloEditDto
            {
                Id = a.id,
                Codigo = a.codigo_articulo,
                Descripcion = a.descripcion,
                UnidadMedidaId = a.unidad_medida_id,
                UnidadAlmacenajeId = a.unidad_almacenaje_id,
                UnidadSalidaId = a.unidad_salida_id,
                TipoArticuloId = a.tipo_articulo_id,
                LineaId = a.linea_id,
                GrupoId = a.grupo_id,
                Diametro = a.diametro,
                CuentaContable = a.cuenta_contable,
                ExistenciaMinima = a.existencia_minima,
                ValorUnitario = a.valor_unitario,
                Existencia = a.existencia
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<string>> GetLineasAsync(CancellationToken ct = default)
    {
        return await _context.alm_articulos
            .AsNoTracking()
            .Where(a => a.linea != null && a.linea != "")
            .Select(a => a.linea!)
            .Distinct()
            .OrderBy(l => l)
            .ToListAsync(ct);
    }

    public async Task<ArticuloEditDto> CreateAsync(ArticuloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeRequired(dto.Codigo, 20, "código", uppercase: true);
        var descripcion = NormalizeRequired(dto.Descripcion, 120, "descripción");

        var exists = await _context.alm_articulos
            .AsNoTracking()
            .AnyAsync(a => a.codigo_articulo == codigo, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un artículo con el código {codigo}.");
        }

        await ValidarUnidadMedidaAsync(dto.UnidadMedidaId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadAlmacenajeId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadSalidaId, ct);

        var entity = new alm_articulo
        {
            codigo_articulo = codigo,
            descripcion = descripcion,
            unidad_medida_id = dto.UnidadMedidaId,
            unidad_almacenaje_id = dto.UnidadAlmacenajeId,
            unidad_salida_id = dto.UnidadSalidaId,
            tipo_articulo_id = dto.TipoArticuloId,
            linea_id = dto.LineaId,
            grupo_id = dto.GrupoId,
            diametro = NormalizeOptional(dto.Diametro, 80),
            cuenta_contable = NormalizeOptional(dto.CuentaContable, 20, uppercase: true),
            existencia_minima = dto.ExistenciaMinima,
            valor_unitario = dto.ValorUnitario,
            existencia = dto.Existencia,
            cantidad = dto.Existencia,
            fecha_registro = DateOnly.FromDateTime(DateTime.Today)
        };

        _context.alm_articulos.Add(entity);
        await _context.SaveChangesAsync(ct);

        // Fase 2: todo artículo nace con una fila en la bodega por defecto (PRIN),
        // que porta la existencia/mínimo inicial. alm_articulo.existencia es el rollup.
        var bodegaPrincipalId = await GetOrCreateBodegaPrincipalAsync(user, ct);
        _context.alm_articulo_bodegas.Add(new alm_articulo_bodega
        {
            articulo_id = entity.id,
            bodega_id = bodegaPrincipalId,
            existencia = dto.Existencia,
            existencia_minima = dto.ExistenciaMinima,
            principal = true,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    private async Task<int> GetOrCreateBodegaPrincipalAsync(string user, CancellationToken ct)
    {
        var existente = await _context.alm_bodegas.AsNoTracking().FirstOrDefaultAsync(b => b.codigo == "PRIN", ct);
        if (existente is not null)
        {
            return existente.id;
        }

        var bodega = new alm_bodega
        {
            codigo = "PRIN",
            nombre = "Bodega principal",
            activo = true,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };
        _context.alm_bodegas.Add(bodega);
        await _context.SaveChangesAsync(ct);
        return bodega.id;
    }

    public async Task<ArticuloEditDto> UpdateAsync(int id, ArticuloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El artículo no es válido.");
        }

        var entity = await _context.alm_articulos.FirstOrDefaultAsync(a => a.id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("El artículo no existe.");
        }

        var codigo = NormalizeRequired(dto.Codigo, 20, "código", uppercase: true);
        var descripcion = NormalizeRequired(dto.Descripcion, 120, "descripción");

        var exists = await _context.alm_articulos
            .AsNoTracking()
            .AnyAsync(a => a.codigo_articulo == codigo && a.id != id, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un artículo con el código {codigo}.");
        }

        await ValidarUnidadMedidaAsync(dto.UnidadMedidaId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadAlmacenajeId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadSalidaId, ct);

        entity.codigo_articulo = codigo;
        entity.descripcion = descripcion;
        entity.unidad_medida_id = dto.UnidadMedidaId;
        entity.unidad_almacenaje_id = dto.UnidadAlmacenajeId;
        entity.unidad_salida_id = dto.UnidadSalidaId;
        entity.tipo_articulo_id = dto.TipoArticuloId;
        entity.linea_id = dto.LineaId;
        entity.grupo_id = dto.GrupoId;
        entity.diametro = NormalizeOptional(dto.Diametro, 80);
        entity.cuenta_contable = NormalizeOptional(dto.CuentaContable, 20, uppercase: true);
        entity.valor_unitario = dto.ValorUnitario;
        // La existencia y el mínimo son rollup de alm_articulo_bodega (se administran
        // por bodega en la pestaña Ubicación); no se sobrescriben desde el form.

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El artículo no es válido.");
        }

        var entity = await _context.alm_articulos.FirstOrDefaultAsync(a => a.id == id, ct);
        if (entity is null)
        {
            return false;
        }

        // No permitir borrar si el artículo tiene movimientos de kardex: se
        // perdería la trazabilidad de existencias.
        var tieneMovimientos = await _context.alm_kardexs
            .AsNoTracking()
            .AnyAsync(k => k.codigo_articulo == entity.codigo_articulo, ct);

        if (tieneMovimientos)
        {
            throw new InvalidOperationException(
                "No se puede eliminar el artículo porque tiene movimientos de kardex registrados.");
        }

        _context.alm_articulos.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} supera {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? trimmed.ToUpperInvariant() : trimmed;
    }

    private static string? NormalizeOptional(string? value, int maxLength, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"El valor supera {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? trimmed.ToUpperInvariant() : trimmed;
    }
}
