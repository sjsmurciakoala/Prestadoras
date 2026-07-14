using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Catálogo de artículos de almacén (alm_articulo). Multiempresa: el filtro
/// global por company_id y el estampado en inserts los aplica SiadDbContext.
/// </summary>
public sealed class ArticulosService : IArticulosService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public ArticulosService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
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
                ValorUnitario = a.valor_unitario,
                ValorTotal = a.ubicaciones.Sum(u => u.existencia)
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new AlertaStockFilterDto();

        // La existencia y el mínimo son POR BODEGA: una alerta es una fila
        // (artículo, bodega) sin stock (o negativa) o por debajo de su mínimo.
        var query = _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.activo)
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

    /// <summary>
    /// Las unidades de medida, almacenaje y salida asignadas al artículo deben tener
    /// categoría (tipo) definida y pertenecer todas a la misma categoría (p. ej. todas
    /// de Peso, o todas de Volumen). No aplica si el artículo no tiene unidades asignadas.
    /// </summary>
    private async Task ValidarCategoriaUnidadesAsync(int? medidaId, int? almacenajeId, int? salidaId, CancellationToken ct)
    {
        var asignadas = new[] { medidaId, almacenajeId, salidaId }
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .ToList();

        if (asignadas.Count == 0)
        {
            return;
        }

        // La unidad de medida es el ancla: define la categoría. No se permite asignar
        // almacenaje o salida sin haber elegido la unidad de medida.
        if (!medidaId.HasValue && (almacenajeId.HasValue || salidaId.HasValue))
        {
            throw new InvalidOperationException("Debe seleccionar la unidad de medida (es la que define la categoría) antes de asignar las unidades de almacenaje o salida.");
        }

        var categoriaPorUnidad = await _context.alm_unidad_medidas.AsNoTracking()
            .Where(u => asignadas.Contains(u.id))
            .Select(u => new { u.id, u.categoria_id })
            .ToListAsync(ct);

        var categorias = asignadas
            .Select(id => categoriaPorUnidad.FirstOrDefault(u => u.id == id)?.categoria_id)
            .ToList();

        if (categorias.Any(c => c is null))
        {
            throw new InvalidOperationException("Las unidades de medida, almacenaje y salida deben tener una categoría (tipo) definida en el catálogo.");
        }

        if (categorias.Distinct().Count() > 1)
        {
            throw new InvalidOperationException("Las unidades de medida, almacenaje y salida deben pertenecer a la misma categoría (por ejemplo, todas de Peso, o todas de Volumen).");
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
                // La existencia mostrada es el total real: la suma de la existencia de cada ubicación (bodega).
                Existencia = a.ubicaciones.Sum(u => u.existencia)
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

        var codigo = NormalizeOptional(dto.Codigo, 20, uppercase: true) ?? string.Empty;
        var descripcion = NormalizeRequired(dto.Descripcion, 120, "descripción");

        // El artículo debe nacer con al menos una bodega (ubicación) elegida por el usuario.
        var ubicaciones = dto.Ubicaciones ?? new List<ArticuloUbicacionDto>();
        if (ubicaciones.Count == 0)
        {
            throw new InvalidOperationException("Debe asignar al menos una bodega (ubicación) al artículo antes de guardarlo.");
        }

        if (ubicaciones.Any(u => u.BodegaId <= 0))
        {
            throw new InvalidOperationException("Todas las ubicaciones deben tener una bodega seleccionada.");
        }

        if (ubicaciones.GroupBy(u => u.BodegaId).Any(g => g.Count() > 1))
        {
            throw new InvalidOperationException("No puede repetir la misma bodega en las ubicaciones del artículo.");
        }

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var exists = await _context.alm_articulos
                .AsNoTracking()
                .AnyAsync(a => a.codigo_articulo == codigo, ct);

            if (exists)
            {
                throw new InvalidOperationException($"Ya existe un artículo con el código {codigo}.");
            }
        }

        await ValidarUnidadMedidaAsync(dto.UnidadMedidaId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadAlmacenajeId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadSalidaId, ct);
        await ValidarCategoriaUnidadesAsync(dto.UnidadMedidaId, dto.UnidadAlmacenajeId, dto.UnidadSalidaId, ct);

        // Todas las bodegas deben existir y estar activas.
        var bodegaIds = ubicaciones.Select(u => u.BodegaId).Distinct().ToList();
        var bodegasValidas = await _context.alm_bodegas.AsNoTracking()
            .Where(b => bodegaIds.Contains(b.id) && b.activo)
            .Select(b => b.id)
            .ToListAsync(ct);
        if (bodegasValidas.Count != bodegaIds.Count)
        {
            throw new InvalidOperationException("Una o más bodegas seleccionadas no existen o están inactivas.");
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var usuario = ClasificacionNormalizer.Usuario(user);

        // Exactamente una principal: la que venga marcada, o la primera si ninguna.
        var indexPrincipal = ubicaciones.FindIndex(u => u.Principal);
        if (indexPrincipal < 0) indexPrincipal = 0;

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
            valor_unitario = dto.ValorUnitario,
            // Existencia y mínimo son el rollup: suma de las filas por bodega.
            existencia = ubicaciones.Sum(u => u.Existencia),
            existencia_minima = ubicaciones.Sum(u => u.ExistenciaMinima),
            cantidad = ubicaciones.Sum(u => u.Existencia),
            fecha_registro = DateOnly.FromDateTime(DateTime.Today),
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        for (var i = 0; i < ubicaciones.Count; i++)
        {
            var u = ubicaciones[i];
            // Insertado por navegación → artículo + filas en una sola transacción (SaveChanges).
            entity.ubicaciones.Add(new alm_articulo_bodega
            {
                bodega_id = u.BodegaId,
                ubicacion1 = ClasificacionNormalizer.Opcional(u.Ubicacion1, 20),
                ubicacion2 = ClasificacionNormalizer.Opcional(u.Ubicacion2, 20),
                ubicacion3 = ClasificacionNormalizer.Opcional(u.Ubicacion3, 20),
                ubicacion4 = ClasificacionNormalizer.Opcional(u.Ubicacion4, 20),
                ubicacion5 = ClasificacionNormalizer.Opcional(u.Ubicacion5, 20),
                existencia = u.Existencia,
                existencia_minima = u.ExistenciaMinima,
                existencia_maxima = u.ExistenciaMaxima,
                principal = i == indexPrincipal,
                activo = true,
                usuariocreacion = usuario,
                fechacreacion = ahora
            });
        }

        // Proveedores ("UPC") opcionales: se insertan junto con el artículo.
        await AgregarProveedoresIniciales(entity, dto.Proveedores, usuario, ahora, ct);

        _context.alm_articulos.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    /// <summary>
    /// Adjunta (por navegación) los proveedores capturados al crear el artículo. Valida que
    /// no se repitan, que existan/activos en la empresa, y que a lo sumo uno sea principal.
    /// </summary>
    private async Task AgregarProveedoresIniciales(
        alm_articulo entity, List<ArticuloProveedorDto>? proveedores, string usuario, DateTime ahora, CancellationToken ct)
    {
        proveedores ??= new List<ArticuloProveedorDto>();
        var items = proveedores
            .Select(p => new { Dto = p, Cod = (p.CodProveedor ?? string.Empty).Trim() })
            .Where(x => x.Cod.Length > 0)
            .ToList();

        if (items.Count == 0)
        {
            return;
        }

        var cods = items.Select(x => x.Cod).ToList();
        if (cods.Distinct().Count() != cods.Count)
        {
            throw new InvalidOperationException("No puede repetir el mismo proveedor en el artículo.");
        }

        var companyId = _company.GetCompanyId();
        var validos = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == companyId && cods.Contains(p.cod_proveedor) && (p.status == null || p.status == true))
            .Select(p => p.cod_proveedor)
            .ToListAsync(ct);

        var invalidos = cods.Where(c => !validos.Contains(c)).ToList();
        if (invalidos.Count > 0)
        {
            throw new InvalidOperationException($"Uno o más proveedores no existen o están inactivos: {string.Join(", ", invalidos)}.");
        }

        // A lo sumo un principal: el primero marcado.
        var indexPrincipal = items.FindIndex(x => x.Dto.Principal);

        for (var i = 0; i < items.Count; i++)
        {
            var x = items[i];
            entity.proveedores.Add(new alm_articulo_proveedor
            {
                cod_proveedor = x.Cod,
                codigo_upc = ClasificacionNormalizer.Opcional(x.Dto.CodigoUpc, 40),
                costo = x.Dto.Costo,
                principal = i == indexPrincipal,
                activo = true,
                usuariocreacion = usuario,
                fechacreacion = ahora
            });
        }
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

        var codigo = NormalizeOptional(dto.Codigo, 20, uppercase: true) ?? string.Empty;
        var descripcion = NormalizeRequired(dto.Descripcion, 120, "descripción");

        if (!string.IsNullOrWhiteSpace(codigo))
        {
            var exists = await _context.alm_articulos
                .AsNoTracking()
                .AnyAsync(a => a.codigo_articulo == codigo && a.id != id, ct);

            if (exists)
            {
                throw new InvalidOperationException($"Ya existe un artículo con el código {codigo}.");
            }
        }

        await ValidarUnidadMedidaAsync(dto.UnidadMedidaId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadAlmacenajeId, ct);
        await ValidarUnidadMedidaAsync(dto.UnidadSalidaId, ct);
        await ValidarCategoriaUnidadesAsync(dto.UnidadMedidaId, dto.UnidadAlmacenajeId, dto.UnidadSalidaId, ct);

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
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
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
        // Se valida por articulo_id (la FK real), NO por codigo_articulo: el código
        // es opcional desde 2026-07-13 y los artículos nuevos lo llevan en blanco,
        // con lo que la comparación por código no encontraría sus movimientos.
        var tieneMovimientos = await _context.alm_kardexs
            .AsNoTracking()
            .AnyAsync(k => k.articulo_id == id, ct);

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
