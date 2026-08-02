using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
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
        var query = AplicarFiltros(_context.alm_articulos.AsNoTracking(), filtro ?? new ArticuloFilterDto());

        return await query
            .OrderBy(a => a.codigo_articulo)
            .Select(Proyeccion)
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ArticuloListItemDto>> SearchPagedAsync(
        ArticuloFilterDto? filtro, int skip, int take, string? sortField, bool sortDesc, CancellationToken ct = default)
    {
        var query = AplicarFiltros(_context.alm_articulos.AsNoTracking(), filtro ?? new ArticuloFilterDto());

        var totalCount = await query.CountAsync(ct);

        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : take;

        var items = await AplicarOrden(query, sortField, sortDesc)
            .Skip(skip)
            .Take(take)
            .Select(Proyeccion)
            .ToListAsync(ct);

        return new PagedResult<ArticuloListItemDto>(items, totalCount);
    }

    public async Task<ArticulosResumenDto> GetResumenAsync(ArticuloFilterDto? filtro, CancellationToken ct = default)
    {
        var query = AplicarFiltros(_context.alm_articulos.AsNoTracking(), filtro ?? new ArticuloFilterDto());

        // Agregados en el servidor sobre el mismo universo filtrado (no la página visible).
        var total = await query.CountAsync(ct);
        var conStock = await query.CountAsync(a => a.existencia > 0, ct);
        var bajoMinimo = await query.CountAsync(a => a.existencia_minima > 0 && a.existencia < a.existencia_minima, ct);
        var valorInventario = await query.SumAsync(a => (decimal?)(a.existencia * a.valor_unitario), ct) ?? 0m;
        var conDescuadre = await query.CountAsync(
            a => a.existencia != a.ubicaciones.Where(u => u.activo).Sum(u => u.existencia), ct);

        return new ArticulosResumenDto
        {
            Total = total,
            ConStock = conStock,
            BajoMinimo = bajoMinimo,
            ValorInventario = valorInventario,
            ConDescuadre = conDescuadre
        };
    }

    /// <summary>Aplica los filtros del grid (search/tipo/categoría/bodega/unidad/estado) a la consulta base.</summary>
    private IQueryable<alm_articulo> AplicarFiltros(IQueryable<alm_articulo> query, ArticuloFilterDto filtro)
    {
        // Soft-delete: por defecto el maestro muestra SOLO los activos. Los descontinuados
        // se ven únicamente si el usuario los pide (para consultarlos o reactivarlos).
        if (filtro.SoloDescontinuados == true)
        {
            query = query.Where(a => !a.activo);
        }
        else if (filtro.IncluirDescontinuados != true)
        {
            query = query.Where(a => a.activo);
        }

        if (filtro.TipoArticuloId.HasValue)
        {
            query = query.Where(a => a.tipo_articulo_id == filtro.TipoArticuloId.Value);
        }

        if (filtro.GrupoId.HasValue)
        {
            query = query.Where(a => a.grupo_id == filtro.GrupoId.Value);
        }

        if (filtro.BodegaId.HasValue)
        {
            query = query.Where(a => a.ubicaciones.Any(u => u.activo && u.bodega_id == filtro.BodegaId.Value));
        }

        if (filtro.SinUnidad == true)
        {
            query = query.Where(a => a.unidad_medida_id == null);
        }

        if (filtro.ConDescuadre == true)
        {
            // Descuadre = la cabecera no cuadra con la Σ de bodegas ACTIVAS. Comparación
            // exacta a propósito: el rollup copia las mismas cifras, no debería haber ruido
            // de redondeo. Se traduce a subconsulta correlacionada cubierta por el índice
            // parcial ix_alm_articulo_bodega_articulo_activo.
            query = query.Where(a => a.existencia != a.ubicaciones.Where(u => u.activo).Sum(u => u.existencia));
        }

        // Estado de existencia: categorías disjuntas (ver EstadoStockFiltro).
        query = filtro.EstadoStock switch
        {
            EstadoStockFiltro.ConStock => query.Where(a =>
                a.existencia > 0 && !(a.existencia_minima > 0 && a.existencia < a.existencia_minima)),
            EstadoStockFiltro.BajoMinimo => query.Where(a =>
                a.existencia > 0 && a.existencia_minima > 0 && a.existencia < a.existencia_minima),
            EstadoStockFiltro.SinStock => query.Where(a => a.existencia == 0),
            EstadoStockFiltro.Negativo => query.Where(a => a.existencia < 0),
            _ => query
        };

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

        return query;
    }

    /// <summary>Ordena por el campo del grid (FieldName del DTO), con el id como desempate estable.</summary>
    private static IOrderedQueryable<alm_articulo> AplicarOrden(IQueryable<alm_articulo> query, string? sortField, bool sortDesc)
    {
        IOrderedQueryable<alm_articulo> ordered = sortField switch
        {
            nameof(ArticuloListItemDto.Id) => sortDesc
                ? query.OrderByDescending(a => a.id) : query.OrderBy(a => a.id),
            nameof(ArticuloListItemDto.Descripcion) => sortDesc
                ? query.OrderByDescending(a => a.descripcion) : query.OrderBy(a => a.descripcion),
            nameof(ArticuloListItemDto.TipoArticuloDisplay) => sortDesc
                ? query.OrderByDescending(a => a.tipo_articulo_ref!.nombre) : query.OrderBy(a => a.tipo_articulo_ref!.nombre),
            nameof(ArticuloListItemDto.UnidadMedidaDisplay) => sortDesc
                ? query.OrderByDescending(a => a.unidad_medida_ref!.codigo) : query.OrderBy(a => a.unidad_medida_ref!.codigo),
            nameof(ArticuloListItemDto.GrupoDisplay) => sortDesc
                ? query.OrderByDescending(a => a.grupo_ref!.nombre) : query.OrderBy(a => a.grupo_ref!.nombre),
            nameof(ArticuloListItemDto.Existencia) => sortDesc
                ? query.OrderByDescending(a => a.existencia) : query.OrderBy(a => a.existencia),
            nameof(ArticuloListItemDto.ValorTotal) => sortDesc
                ? query.OrderByDescending(a => a.existencia * a.valor_unitario)
                : query.OrderBy(a => a.existencia * a.valor_unitario),
            _ => sortDesc
                ? query.OrderByDescending(a => a.codigo_articulo) : query.OrderBy(a => a.codigo_articulo)
        };

        return sortDesc ? ordered.ThenByDescending(a => a.id) : ordered.ThenBy(a => a.id);
    }

    /// <summary>Proyección estándar de artículo a item de lista (reutilizada por GetAsync y SearchPagedAsync).</summary>
    private static readonly Expression<Func<alm_articulo, ArticuloListItemDto>> Proyeccion = a => new ArticuloListItemDto
    {
        Id = a.id,
        Codigo = a.codigo_articulo,
        Descripcion = a.descripcion,
        UnidadMedida = a.unidad_medida,
        UnidadMedidaId = a.unidad_medida_id,
        UnidadMedidaCodigo = a.unidad_medida_ref != null ? a.unidad_medida_ref.codigo : null,
        TipoArticuloNombre = a.tipo_articulo_ref != null ? a.tipo_articulo_ref.nombre : null,
        Grupo = a.grupo,
        GrupoNombre = a.grupo_ref != null ? a.grupo_ref.nombre : null,
        Diametro = a.diametro,
        CuentaContable = a.cuenta_contable,
        Existencia = a.existencia,
        ExistenciaMinima = a.existencia_minima,
        ValorUnitario = a.valor_unitario,
        // DINERO, misma fórmula que el KPI ValorInventario: la suma de la columna cuadra
        // con la tarjeta por construcción. (Antes era Σ existencias de ubicaciones — una
        // cantidad — y además sumaba las inactivas.)
        ValorTotal = a.existencia * a.valor_unitario,
        // Σ de bodegas ACTIVAS (contrato del rollup): alimenta el detector de descuadre.
        ExistenciaBodegas = a.ubicaciones.Where(u => u.activo).Sum(u => u.existencia),
        Activo = a.activo
    };

    public async Task<IReadOnlyList<AlertaStockDto>> GetAlertasStockAsync(AlertaStockFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new AlertaStockFilterDto();

        // La existencia y el mínimo son POR BODEGA: una alerta es una fila
        // (artículo, bodega) sin stock (o negativa) o por debajo de su mínimo.
        // Se excluyen los artículos DESCONTINUADOS (soft-delete, 2026-07-29): pedir
        // reposición de algo que se dio de baja a propósito es ruido puro. Ojo: u.activo
        // es la UBICACIÓN; el estado del artículo es u.articulo.activo.
        var query = _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.activo)
            .Where(u => u.articulo != null && u.articulo.activo)
            .Where(u => u.existencia <= 0
                     || (u.existencia_minima > 0 && u.existencia < u.existencia_minima));

        if (filtro.SoloConMinimo == true)
        {
            query = query.Where(u => u.existencia_minima > 0);
        }

        if (filtro.TipoArticuloId.HasValue)
        {
            query = query.Where(u => u.articulo != null && u.articulo.tipo_articulo_id == filtro.TipoArticuloId.Value);
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
                TipoArticuloNombre = u.articulo != null && u.articulo.tipo_articulo_ref != null
                    ? u.articulo.tipo_articulo_ref.nombre : null,
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
                TipoArticuloNombre = a.TipoArticuloNombre,
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

    /// <summary>
    /// Valida que el tipo de artículo exista (en la empresa actual: el filtro global por
    /// company_id lo garantiza) y devuelve su maneja_inventario. El tipo es OBLIGATORIO
    /// desde la unificación línea→tipo (2026-07-16): un tipo ausente o inexistente
    /// (incluido el cross-tenant) se rechaza, no se asume nada.
    /// </summary>
    private async Task<bool> ValidarTipoArticuloAsync(int? tipoArticuloId, CancellationToken ct)
    {
        if (!tipoArticuloId.HasValue)
        {
            throw new InvalidOperationException("Debe seleccionar el tipo de artículo.");
        }

        var tipo = await _context.alm_tipo_articulos.AsNoTracking()
            .Where(t => t.id == tipoArticuloId.Value)
            .Select(t => new { t.maneja_inventario, t.activo })
            .FirstOrDefaultAsync(ct);

        if (tipo is null)
        {
            throw new InvalidOperationException("El tipo de artículo seleccionado no existe.");
        }

        if (!tipo.activo)
        {
            throw new InvalidOperationException("El tipo de artículo seleccionado está inactivo.");
        }

        return tipo.maneja_inventario;
    }

    /// <summary>
    /// La categoría (alm_grupo) es opcional, pero si viene debe existir y pertenecer al
    /// tipo de artículo elegido. Antes esta coherencia vivía sólo en la UI; un POST
    /// directo a la API podía guardar cualquier combinación.
    /// </summary>
    private async Task ValidarGrupoAsync(int? grupoId, int tipoArticuloId, CancellationToken ct)
    {
        if (!grupoId.HasValue)
        {
            return;
        }

        var grupo = await _context.alm_grupos.AsNoTracking()
            .Where(g => g.id == grupoId.Value)
            .Select(g => new { g.tipo_articulo_id })
            .FirstOrDefaultAsync(ct);

        if (grupo is null)
        {
            throw new InvalidOperationException("La categoría seleccionada no existe.");
        }

        if (grupo.tipo_articulo_id != tipoArticuloId)
        {
            throw new InvalidOperationException("La categoría seleccionada no pertenece al tipo de artículo elegido.");
        }
    }

    /// <summary>
    /// La cuenta contable del artículo es opcional, pero si viene debe existir en el plan
    /// de cuentas de la empresa, ser imputable (allows_posting) y estar activa — el MISMO
    /// criterio con el que el combo del formulario filtra el plan
    /// (ArticulosClient.GetCuentasContablesAsync), para que el servidor acepte exactamente
    /// lo que la UI ofrece. Antes esta coherencia vivía solo en la UI: un POST directo
    /// podía guardar cualquier texto. Los códigos se comparan sin separadores
    /// (AccountCodeFormatter): puntos/guiones son máscara de presentación, no código.
    /// </summary>
    private async Task ValidarCuentaContableAsync(string? cuentaContable, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(cuentaContable))
        {
            return; // opcional: los históricos SIMAFI sin cuenta siguen siendo válidos
        }

        var codigo = AccountCodeFormatter.Normalize(cuentaContable);

        // El filtro global por company_id ya acota el plan al tenant actual.
        var cuenta = await _context.con_plan_cuentas.AsNoTracking()
            .Where(c => c.code == codigo)
            .Select(c => new { c.allows_posting, c.status })
            .FirstOrDefaultAsync(ct);

        if (cuenta is null)
        {
            throw new InvalidOperationException(
                $"La cuenta contable {cuentaContable} no existe en el plan de cuentas de la empresa.");
        }

        if (!cuenta.allows_posting)
        {
            throw new InvalidOperationException(
                $"La cuenta contable {cuentaContable} no permite movimientos (no es imputable): elija una cuenta de detalle.");
        }

        var activa = string.IsNullOrWhiteSpace(cuenta.status)
            || string.Equals(cuenta.status, "ACTIVE", StringComparison.OrdinalIgnoreCase)
            || string.Equals(cuenta.status, "ACTIVO", StringComparison.OrdinalIgnoreCase);

        if (!activa)
        {
            throw new InvalidOperationException(
                $"La cuenta contable {cuentaContable} está inactiva en el plan de cuentas.");
        }
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
                GrupoId = a.grupo_id,
                Diametro = a.diametro,
                CuentaContable = a.cuenta_contable,
                ExistenciaMinima = a.existencia_minima,
                ValorUnitario = a.valor_unitario,
                Activo = a.activo,
                // xmin es la propiedad sombra del token de concurrencia (UseXminAsConcurrencyToken).
                // Viaja al cliente para que el PUT pueda detectar que otro usuario guardó primero.
                RowVersion = EF.Property<uint>(a, "xmin"),
                // La existencia mostrada es el total real: la suma de las ubicaciones ACTIVAS
                // (mismo contrato que el rollup de cabecera; antes sumaba también las
                // deshabilitadas y el form contradecía al maestro).
                Existencia = a.ubicaciones.Where(u => u.activo).Sum(u => u.existencia)
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ArticuloEditDto> CreateAsync(ArticuloEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = NormalizeOptional(dto.Codigo, 20, uppercase: true) ?? string.Empty;
        var descripcion = NormalizeRequired(dto.Descripcion, 120, "descripción");

        // El tipo es obligatorio y define si el artículo lleva existencias: con
        // maneja_inventario=false (p. ej. "Servicios") NO lleva bodega, ni ubicación,
        // ni kardex. La regla vive aquí (capa de servicio), no en la BD: un POST
        // directo a la API saltándose la UI también queda rechazado.
        var manejaInventario = await ValidarTipoArticuloAsync(dto.TipoArticuloId, ct);
        await ValidarGrupoAsync(dto.GrupoId, dto.TipoArticuloId!.Value, ct);

        // Un artículo que lleva existencias necesita unidad de medida: sin ella el kardex
        // muestra cantidades sin unidad y no hay forma de convertir almacenaje/salida.
        // Se exige SOLO AL CREAR (2026-07-29): los artículos migrados de SIMAFI tienen
        // unidad_medida_id NULL y exigirla al editar dejaría bloqueado a quien solo quiere
        // corregirles la descripción. Esos se sanean con el filtro "Sin unidad" del maestro.
        if (manejaInventario && !dto.UnidadMedidaId.HasValue)
        {
            throw new InvalidOperationException(
                "Debe seleccionar la unidad de medida: el tipo de artículo elegido maneja inventario.");
        }

        var ubicaciones = dto.Ubicaciones ?? new List<ArticuloUbicacionDto>();

        if (!manejaInventario)
        {
            // Un servicio no puede tener bodega: se rechaza en vez de ignorar en silencio.
            if (ubicaciones.Count > 0)
            {
                throw new InvalidOperationException(
                    "El tipo de artículo seleccionado no maneja inventario (ej. Servicios): el artículo no puede tener bodegas ni ubicaciones.");
            }
        }
        else
        {
            // El artículo debe nacer con al menos una bodega (ubicación) elegida por el usuario.
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

        // Al CREAR la cuenta contable se valida siempre (si viene).
        var cuentaContable = NormalizeOptional(dto.CuentaContable, 20, uppercase: true);
        await ValidarCuentaContableAsync(cuentaContable, ct);

        // Todas las bodegas deben existir y estar activas. (Sin inventario no hay bodegas que validar.)
        if (manejaInventario)
        {
            var bodegaIds = ubicaciones.Select(u => u.BodegaId).Distinct().ToList();
            var bodegasValidas = await _context.alm_bodegas.AsNoTracking()
                .Where(b => bodegaIds.Contains(b.id) && b.activo)
                .Select(b => b.id)
                .ToListAsync(ct);
            if (bodegasValidas.Count != bodegaIds.Count)
            {
                throw new InvalidOperationException("Una o más bodegas seleccionadas no existen o están inactivas.");
            }
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
            grupo_id = dto.GrupoId,
            diametro = NormalizeOptional(dto.Diametro, 80),
            cuenta_contable = cuentaContable,
            valor_unitario = dto.ValorUnitario,
            // Existencia y mínimo son el rollup: suma de las filas por bodega.
            existencia = ubicaciones.Sum(u => u.Existencia),
            existencia_minima = ubicaciones.Sum(u => u.ExistenciaMinima),
            cantidad = ubicaciones.Sum(u => u.Existencia),
            fecha_registro = DateOnly.FromDateTime(DateTime.Today),
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        // Sin inventario la lista está vacía (ya se rechazó arriba si venía con filas).
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
                punto_reorden = u.PuntoReorden,
                // comprometida / tránsito / costo promedio / último costo: los mueve el motor
                // de posteo, nunca el DTO. Nacen en 0 por DEFAULT.
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
                // El costo ya no se captura por proveedor (se lleva en Existencias); la
                // columna alm_articulo_proveedor.costo sigue en la BD pero no se escribe.
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

        // Concurrencia optimista: el cliente devuelve el xmin que leyó al abrir el artículo.
        // Al fijarlo como OriginalValue, EF lo incluye en el WHERE del UPDATE; si otro usuario
        // guardó en el intervalo, no afecta filas y EF lanza DbUpdateConcurrencyException
        // (el controlador la traduce a 409) en vez de pisar el cambio ajeno en silencio.
        if (dto.RowVersion.HasValue)
        {
            _context.Entry(entity).Property("xmin").OriginalValue = dto.RowVersion.Value;
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

        var manejaInventario = await ValidarTipoArticuloAsync(dto.TipoArticuloId, ct);
        await ValidarGrupoAsync(dto.GrupoId, dto.TipoArticuloId!.Value, ct);

        // Cambiar el tipo a uno SIN inventario sólo es válido si el artículo no tiene rastro
        // de existencias: si ya tiene bodegas asignadas o movimientos de kardex, convertirlo
        // en "servicio" dejaría filas y asientos huérfanos. Se rechaza y se le dice al usuario
        // qué debe hacer primero.
        if (!manejaInventario)
        {
            if (await _context.alm_articulo_bodegas.AsNoTracking().AnyAsync(u => u.articulo_id == id, ct))
            {
                throw new InvalidOperationException(
                    "No se puede cambiar el artículo a un tipo que no maneja inventario: todavía tiene bodegas asignadas. Elimine sus ubicaciones primero.");
            }

            if (await _context.alm_kardexs.AsNoTracking().AnyAsync(k => k.articulo_id == id, ct))
            {
                throw new InvalidOperationException(
                    "No se puede cambiar el artículo a un tipo que no maneja inventario: ya tiene movimientos de kardex registrados.");
            }
        }

        entity.codigo_articulo = codigo;
        entity.descripcion = descripcion;
        entity.unidad_medida_id = dto.UnidadMedidaId;
        entity.unidad_almacenaje_id = dto.UnidadAlmacenajeId;
        entity.unidad_salida_id = dto.UnidadSalidaId;
        entity.tipo_articulo_id = dto.TipoArticuloId;
        entity.grupo_id = dto.GrupoId;
        entity.diametro = NormalizeOptional(dto.Diametro, 80);

        // La cuenta se valida SOLO si CAMBIA (comparando ambos lados sin separadores):
        // un re-save del form —que siempre reenvía CuentaContable— no debe reventar en un
        // artículo histórico SIMAFI cuya cuenta ya no exista en el plan. Mismo criterio
        // que la unidad de medida (exigencia solo hacia adelante).
        var cuentaNueva = NormalizeOptional(dto.CuentaContable, 20, uppercase: true);
        if (!string.Equals(
                AccountCodeFormatter.Normalize(cuentaNueva),
                AccountCodeFormatter.Normalize(entity.cuenta_contable),
                StringComparison.OrdinalIgnoreCase))
        {
            await ValidarCuentaContableAsync(cuentaNueva, ct);
            entity.cuenta_contable = cuentaNueva;
        }
        // Si es la misma cuenta (aunque cambie el formato: guiones/puntos), se conserva
        // el valor almacenado tal cual: no hay cambio real que validar ni escribir.
        entity.valor_unitario = dto.ValorUnitario;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        // La existencia y el mínimo son rollup de alm_articulo_bodega (se administran
        // por bodega en la pestaña Ubicación); no se sobrescriben desde el form.

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        return dto;
    }

    /// <summary>
    /// DESCONTINÚA el artículo (soft-delete, 2026-07-29). Ya NO borra físicamente: el
    /// artículo se conserva, su kardex sigue consultable y deja de ofrecerse para
    /// documentos nuevos. La bitácora lo registra como "Eliminación" porque el
    /// interceptor de auditoría interpreta el cambio de activo true→false como borrado.
    /// Devuelve false si el artículo no existe; true si quedó descontinuado.
    /// </summary>
    public async Task<bool> DeleteAsync(int id, string user, CancellationToken ct = default)
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

        if (!entity.activo)
        {
            throw new InvalidOperationException("El artículo ya está descontinuado.");
        }

        // Antes se bloqueaba el borrado cuando había movimientos de kardex, porque el
        // DELETE físico rompía la trazabilidad. Con soft-delete eso ya no aplica: tener
        // historia es justamente la razón de conservarlo, así que se descontinúa igual.
        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Reactiva un artículo descontinuado: vuelve a estar disponible para documentos nuevos.
    /// Valida que su clasificación siga vigente (el tipo pudo desactivarse mientras estaba fuera).
    /// </summary>
    public async Task<bool> ReactivarAsync(int id, string user, CancellationToken ct = default)
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

        if (entity.activo)
        {
            throw new InvalidOperationException("El artículo ya está activo.");
        }

        // Si el artículo TIENE tipo, este pudo desactivarse mientras estaba descontinuado:
        // reactivarlo con un tipo inactivo lo dejaría inconsistente. En cambio, un artículo
        // histórico SIN tipo (tipo_articulo_id es nullable por los datos migrados) sí se
        // puede reactivar: exigirle un tipo aquí impediría deshacer un descontinuado por
        // error, y el tipo ya se exige al editarlo o crearlo.
        if (entity.tipo_articulo_id.HasValue)
        {
            var tipoVigente = await _context.alm_tipo_articulos.AsNoTracking()
                .Where(t => t.id == entity.tipo_articulo_id.Value)
                .Select(t => (bool?)t.activo)
                .FirstOrDefaultAsync(ct);

            if (tipoVigente is null)
            {
                throw new InvalidOperationException("No se puede reactivar: el tipo de artículo ya no existe. Edita el artículo y asígnale un tipo vigente.");
            }

            if (tipoVigente == false)
            {
                throw new InvalidOperationException("No se puede reactivar: el tipo de artículo está inactivo. Reactiva el tipo o asígnale otro al artículo.");
            }
        }

        entity.activo = true;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

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
