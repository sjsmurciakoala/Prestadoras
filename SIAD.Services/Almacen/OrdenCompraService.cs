using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Órdenes de compra. Ver <see cref="IOrdenCompraService"/>. La numeración es un
/// correlativo por empresa (alm_orden_compra_correlativo) resuelto con un
/// INSERT … ON CONFLICT … RETURNING atómico dentro de la transacción del alta.
/// </summary>
public sealed class OrdenCompraService : IOrdenCompraService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly ITasaIsvArticuloResolver _tasas;

    public OrdenCompraService(
        SiadDbContext context, ICurrentCompanyService company, ITasaIsvArticuloResolver tasas)
    {
        _context = context;
        _company = company;
        _tasas = tasas;
    }

    public async Task<IReadOnlyList<OrdenCompraListItemDto>> GetAsync(OrdenCompraFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new OrdenCompraFilterDto();

        var query = _context.alm_orden_compras.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.CodProveedor))
        {
            var cod = filtro.CodProveedor.Trim();
            query = query.Where(o => o.cod_proveedor == cod);
        }
        if (filtro.Estado.HasValue)
        {
            query = query.Where(o => o.estado == filtro.Estado.Value);
        }
        if (filtro.FechaDesde.HasValue)
        {
            query = query.Where(o => o.fecha != null && o.fecha >= filtro.FechaDesde.Value);
        }
        if (filtro.FechaHasta.HasValue)
        {
            query = query.Where(o => o.fecha != null && o.fecha <= filtro.FechaHasta.Value);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var like = $"%{filtro.Search.Trim()}%";
            query = query.Where(o =>
                EF.Functions.ILike(o.cod_proveedor, like) ||
                EF.Functions.ILike(o.observaciones ?? string.Empty, like) ||
                o.numero.ToString().Contains(filtro.Search.Trim()));
        }

        var filas = await query
            .OrderByDescending(o => o.fecha)
            .ThenByDescending(o => o.numero)
            .Select(o => new OrdenCompraListItemDto
            {
                Id = o.id,
                Numero = o.numero,
                Fecha = o.fecha,
                FechaEntregaPactada = o.fecha_entrega_pactada,
                CodProveedor = o.cod_proveedor,
                Total = o.total,
                Estado = o.estado,
                Renglones = o.detalles.Count
            })
            .ToListAsync(ct);

        await ResolverProveedoresAsync(filas.Select(f => f.CodProveedor),
            (cod, nombre) => { foreach (var f in filas.Where(x => x.CodProveedor == cod)) f.ProveedorNombre = nombre; }, ct);

        return filas;
    }

    public async Task<OrdenCompraDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var o = await _context.alm_orden_compras.AsNoTracking()
            .Include(x => x.detalles)
            .FirstOrDefaultAsync(x => x.id == id, ct);
        if (o is null) return null;

        var dto = MapCabecera(o);

        // Código de artículo por renglón (para mostrar).
        var articuloIds = o.detalles.Select(d => d.articulo_id).Distinct().ToList();
        var codigos = await _context.alm_articulos.AsNoTracking()
            .Where(a => articuloIds.Contains(a.id))
            .Select(a => new { a.id, a.codigo_articulo })
            .ToDictionaryAsync(a => a.id, a => a.codigo_articulo, ct);

        dto.Detalles = o.detalles
            .OrderBy(d => d.id)
            .Select(d => new OrdenCompraDetalleDto
            {
                Id = d.id,
                ArticuloId = d.articulo_id,
                CodigoArticulo = codigos.TryGetValue(d.articulo_id, out var c) ? c : null,
                CodigoUpc = d.codigo_upc,
                CentroCosto = d.centro_costo,
                Descripcion = d.descripcion,
                CantidadPedida = d.cantidad_pedida,
                CostoUnitario = d.costo_unitario,
                Impuesto = d.impuesto,
                Total = d.total,
                CantidadAplicada = d.cantidad_aplicada,
                FechaEntregaPactada = d.fecha_entrega_pactada
            })
            .ToList();

        // Tasa de ISV por renglón (capa 1): alimenta el espejo de la pantalla y el aviso de
        // "sin ISV configurado". Se evalúa a la fecha de la orden, igual que al guardar.
        var fechaOrden = o.fecha ?? DateOnly.FromDateTime(DateTime.Today);
        var tasas = await _tasas.ResolverAsync(
            o.detalles.Select(d => d.articulo_id).ToList(), fechaOrden, ct);
        foreach (var det in dto.Detalles)
        {
            if (tasas.TryGetValue(det.ArticuloId, out var t))
            {
                det.TasaIsv = t.Porcentaje;
                det.IsvConfigurado = t.TipoTieneTasa;
                det.TipoArticuloNombre = t.TipoNombre;
            }
        }

        await ResolverProveedoresAsync(new[] { dto.CodProveedor },
            (cod, nombre) => dto.ProveedorNombre = nombre, ct);

        return dto;
    }

    public async Task<OrdenCompraDto> CrearAsync(OrdenCompraDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        var cod = NormalizarCodProveedor(dto.CodProveedor);
        // La fecha de la orden se resuelve ANTES de validar: es la referencia contra la que se
        // acota la fecha de entrega pactada (cabecera y renglones).
        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);
        ValidarCabecera(dto, fecha);
        await ValidarProveedorAsync(cod, companyId, ct);
        var upcs = await ValidarRenglonesAsync(dto.Detalles, cod, fecha, ct);

        // El ISV lo calcula el servidor desde la tasa del tipo de artículo (capa 1), vigente a
        // la fecha de la orden: NO se confía en el impuesto que mande el cliente.
        var tasas = await _tasas.ResolverAsync(
            dto.Detalles.Select(d => d.ArticuloId).ToList(), fecha, ct);

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // TransaccionAmbiente, no BeginTransactionAsync: los tests envuelven cada prueba en
        // BEGIN … ROLLBACK y anidar no está soportado (ver TransaccionAmbiente).
        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var numero = await SiguienteNumeroAsync(companyId, ct);

        var entity = new alm_orden_compra
        {
            numero = numero,
            fecha = fecha,
            fecha_emision = dto.FechaEmision,
            fecha_entrega_pactada = dto.FechaEntregaPactada,
            cod_proveedor = cod,
            terminos_pago = ClasificacionNormalizer.Opcional(dto.TerminosPago, 100),
            destino_uso = ClasificacionNormalizer.Opcional(dto.DestinoUso, 250),
            calcula_isv = dto.CalculaIsv,
            descuento = dto.Descuento,
            otros_gastos = dto.OtrosGastos,
            estado = EstadoOrdenCompra.Borrador,
            observaciones = ClasificacionNormalizer.Opcional(dto.Observaciones, 1000),
            requisicion_id = dto.RequisicionId,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        AplicarRenglonesYTotales(entity, dto, upcs, tasas);

        _context.alm_orden_compras.Add(entity);
        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return (await GetByIdAsync(entity.id, ct))!;
    }

    public async Task<OrdenCompraDto> ActualizarAsync(int id, OrdenCompraDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_orden_compras
            .Include(x => x.detalles)
            .FirstOrDefaultAsync(x => x.id == id, ct)
            ?? throw new KeyNotFoundException("La orden de compra no existe.");

        if (entity.estado != EstadoOrdenCompra.Borrador)
        {
            throw new InvalidOperationException("Solo se puede editar una orden en Borrador.");
        }

        var companyId = _company.GetCompanyId();
        var cod = NormalizarCodProveedor(dto.CodProveedor);
        var fecha = dto.Fecha ?? entity.fecha ?? DateOnly.FromDateTime(DateTime.Today);
        ValidarCabecera(dto, fecha);
        await ValidarProveedorAsync(cod, companyId, ct);
        var upcs = await ValidarRenglonesAsync(dto.Detalles, cod, fecha, ct);

        // Igual que en el alta: el ISV se recalcula desde la tasa del tipo, a la fecha de la orden.
        var tasas = await _tasas.ResolverAsync(
            dto.Detalles.Select(d => d.ArticuloId).ToList(), fecha, ct);

        entity.cod_proveedor = cod;
        entity.fecha = fecha;
        entity.fecha_emision = dto.FechaEmision;
        entity.fecha_entrega_pactada = dto.FechaEntregaPactada;
        entity.terminos_pago = ClasificacionNormalizer.Opcional(dto.TerminosPago, 100);
        entity.destino_uso = ClasificacionNormalizer.Opcional(dto.DestinoUso, 250);
        entity.calcula_isv = dto.CalculaIsv;
        entity.descuento = dto.Descuento;
        entity.otros_gastos = dto.OtrosGastos;
        entity.observaciones = ClasificacionNormalizer.Opcional(dto.Observaciones, 1000);
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // Reemplazo total de renglones (la orden en Borrador se re-arma).
        _context.alm_orden_compra_detalles.RemoveRange(entity.detalles);
        entity.detalles.Clear();
        AplicarRenglonesYTotales(entity, dto, upcs, tasas);

        await _context.SaveChangesAsync(ct);
        return (await GetByIdAsync(entity.id, ct))!;
    }

    public async Task<bool> AprobarAsync(int id, string user, CancellationToken ct = default)
    {
        var entity = await _context.alm_orden_compras.FirstOrDefaultAsync(x => x.id == id, ct);
        if (entity is null) return false;

        if (entity.estado == EstadoOrdenCompra.Aprobada) return true;
        if (entity.estado != EstadoOrdenCompra.Borrador)
        {
            throw new InvalidOperationException("Solo se puede aprobar una orden en Borrador.");
        }
        if (!await _context.alm_orden_compra_detalles.AsNoTracking().AnyAsync(d => d.orden_compra_id == id, ct))
        {
            throw new InvalidOperationException("No se puede aprobar una orden sin renglones.");
        }

        entity.estado = EstadoOrdenCompra.Aprobada;
        entity.aprobado_por = ClasificacionNormalizer.Usuario(user);
        entity.fecha_aprobacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        entity.usuariomodificacion = entity.aprobado_por;
        entity.fechamodificacion = entity.fecha_aprobacion;
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AnularAsync(int id, string user, CancellationToken ct = default)
    {
        var entity = await _context.alm_orden_compras.FirstOrDefaultAsync(x => x.id == id, ct);
        if (entity is null) return false;
        if (entity.estado == EstadoOrdenCompra.Anulada) return true;
        if (entity.estado is EstadoOrdenCompra.RecibidaParcial or EstadoOrdenCompra.Cerrada)
        {
            throw new InvalidOperationException("No se puede anular una orden que ya tiene recepciones.");
        }
        if (await _context.alm_orden_compra_detalles.AsNoTracking()
                .AnyAsync(d => d.orden_compra_id == id && d.cantidad_aplicada > 0, ct))
        {
            throw new InvalidOperationException("No se puede anular una orden con renglones ya recibidos.");
        }

        entity.estado = EstadoOrdenCompra.Anulada;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<IReadOnlyList<OrdenCompraArticuloLookupDto>> BuscarArticulosProveedorAsync(
        string codProveedor, string? search, CancellationToken ct = default)
    {
        var cod = (codProveedor ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(cod))
        {
            return Array.Empty<OrdenCompraArticuloLookupDto>();
        }

        // Misma regla que ValidarRenglonesAsync: relación activa Y con código de proveedor.
        var query = from p in _context.alm_articulo_proveedors.AsNoTracking()
                    join a in _context.alm_articulos.AsNoTracking() on p.articulo_id equals a.id
                    where p.cod_proveedor == cod
                       && p.activo
                       && p.codigo_upc != null
                       && p.codigo_upc != ""
                       && a.activo
                    select new OrdenCompraArticuloLookupDto
                    {
                        ArticuloId = a.id,
                        CodigoArticulo = a.codigo_articulo,
                        Descripcion = a.descripcion,
                        CodigoUpc = p.codigo_upc!,
                        Costo = p.costo,
                        UnidadMedida = a.unidad_medida
                    };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var like = $"%{search.Trim()}%";
            query = query.Where(x =>
                EF.Functions.ILike(x.CodigoArticulo ?? string.Empty, like) ||
                EF.Functions.ILike(x.Descripcion, like) ||
                EF.Functions.ILike(x.CodigoUpc, like));
        }

        var lista = await query
            .OrderBy(x => x.CodigoArticulo)
            .Take(200)
            .ToListAsync(ct);

        // Tasa de ISV propuesta (capa 1, vigente hoy) + si el tipo la tiene configurada, para
        // avisar en pantalla cuando un artículo iría sin ISV por falta de configuración.
        if (lista.Count > 0)
        {
            var hoy = DateOnly.FromDateTime(DateTime.Today);
            var tasas = await _tasas.ResolverAsync(lista.Select(x => x.ArticuloId).ToList(), hoy, ct);
            foreach (var item in lista)
            {
                if (tasas.TryGetValue(item.ArticuloId, out var t))
                {
                    item.TasaIsv = t.Porcentaje;
                    item.TieneIsvConfigurado = t.TipoTieneTasa;
                    item.TipoArticuloNombre = t.TipoNombre;
                }
            }
        }

        return lista;
    }

    // ── Piezas internas ──────────────────────────────────────────────────────

    /// <summary>
    /// Siguiente número de la empresa. Dos pasos, ambos dentro de la transacción del alta:
    /// <list type="number">
    /// <item>alta idempotente de la fila del contador (<c>ON CONFLICT DO NOTHING</c>), para
    /// que la primera O/C de una empresa no dependa de una semilla previa;</item>
    /// <item><c>SELECT … FOR UPDATE</c> sobre esa fila: el candado serializa dos altas
    /// concurrentes, que si no calcularían el mismo número y chocarían contra
    /// <c>uq_alm_orden_compra_numero</c>.</item>
    /// </list>
    /// El <c>company_id</c> va DENTRO del SQL crudo (mismo motivo que en
    /// <c>InventarioPostingService</c>: EF compone su filtro de tenant por encima, así que
    /// sin esto el candado se tomaría antes de filtrar por empresa). El incremento lo
    /// persiste el <c>SaveChanges</c> del alta, no una escritura aparte.
    /// </summary>
    private async Task<int> SiguienteNumeroAsync(long companyId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alm_orden_compra_correlativo (company_id, ultimo_numero)
            VALUES ({companyId}, 0)
            ON CONFLICT (company_id) DO NOTHING", ct);

        var contador = await _context.alm_orden_compra_correlativos
            .FromSqlInterpolated($@"
                SELECT * FROM alm_orden_compra_correlativo
                 WHERE company_id = {companyId}
                 FOR UPDATE")
            .FirstAsync(ct);

        contador.ultimo_numero += 1;
        return contador.ultimo_numero;
    }

    /// <summary>
    /// Arma los renglones sobre la entidad y calcula subtotal/impuesto/total de cabecera.
    /// <para>
    /// El <c>codigo_upc</c> se toma de <paramref name="upcs"/> (la fila de
    /// <c>alm_articulo_proveedor</c> que ya se validó), NO del DTO: el snapshot debe ser el
    /// código real con que el proveedor identifica el artículo, no lo que mande el cliente.
    /// </para>
    /// <para>
    /// El ISV de cada renglón se CALCULA desde la tasa del tipo de artículo
    /// (<paramref name="tasas"/>) × total del renglón; el impuesto que venga en el DTO se
    /// ignora. Si la orden no calcula ISV, o el artículo no tiene tasa, el renglón va sin ISV.
    /// </para>
    /// </summary>
    private static void AplicarRenglonesYTotales(
        alm_orden_compra entity, OrdenCompraDto dto, IReadOnlyDictionary<int, string> upcs,
        IReadOnlyDictionary<int, TasaIsvArticulo> tasas)
    {
        decimal subTotal = 0m, impuesto = 0m;
        foreach (var d in dto.Detalles)
        {
            var totalRenglon = Redondear2(d.CantidadPedida * d.CostoUnitario);
            var pct = entity.calcula_isv && tasas.TryGetValue(d.ArticuloId, out var t) ? t.Porcentaje : 0m;
            var isvRenglon = Redondear2(totalRenglon * pct / 100m);
            ExigirMonto(totalRenglon, "El total de un renglón");
            ExigirMonto(isvRenglon, "El impuesto de un renglón");
            subTotal += totalRenglon;
            impuesto += isvRenglon;

            entity.detalles.Add(new alm_orden_compra_detalle
            {
                articulo_id = d.ArticuloId,
                codigo_upc = upcs.TryGetValue(d.ArticuloId, out var upc) ? upc : null,
                centro_costo = ClasificacionNormalizer.Opcional(d.CentroCosto, 40),
                descripcion = ClasificacionNormalizer.Opcional(d.Descripcion, 250),
                cantidad_pedida = d.CantidadPedida,
                costo_unitario = d.CostoUnitario,
                impuesto = isvRenglon,
                total = totalRenglon,
                cantidad_aplicada = 0m,
                // NULL a propósito cuando el renglón no trae fecha propia: la de la cabecera
                // rige, y guardarla duplicada aquí haría que editar la orden dejara renglones
                // con la fecha vieja.
                fecha_entrega_pactada = d.FechaEntregaPactada
            });
        }

        // descuento está acotado a 0..100 por ValidarCabecera, así que el total no puede
        // salir negativo por esta vía.
        var montoDescuento = Redondear2(subTotal * entity.descuento / 100m);
        entity.sub_total = Redondear2(subTotal);
        entity.impuesto = Redondear2(impuesto);
        entity.total = Redondear2(subTotal - montoDescuento + impuesto + entity.otros_gastos);

        ExigirMonto(entity.sub_total, "El subtotal de la orden");
        ExigirMonto(entity.impuesto, "El impuesto de la orden");
        ExigirMonto(entity.total, "El total de la orden");
    }

    private static OrdenCompraDto MapCabecera(alm_orden_compra o) => new()
    {
        Id = o.id,
        Numero = o.numero,
        Fecha = o.fecha,
        FechaEmision = o.fecha_emision,
        FechaEntregaPactada = o.fecha_entrega_pactada,
        CodProveedor = o.cod_proveedor,
        TerminosPago = o.terminos_pago,
        DestinoUso = o.destino_uso,
        CalculaIsv = o.calcula_isv,
        Descuento = o.descuento,
        OtrosGastos = o.otros_gastos,
        SubTotal = o.sub_total,
        Impuesto = o.impuesto,
        Total = o.total,
        Estado = o.estado,
        Observaciones = o.observaciones,
        RequisicionId = o.requisicion_id,
        AprobadoPor = o.aprobado_por,
        FechaAprobacion = o.fecha_aprobacion
    };

    private async Task ValidarProveedorAsync(string cod, long companyId, CancellationToken ct)
    {
        var existe = await _context.prv_proveedores.AsNoTracking()
            .AnyAsync(p => p.company_id == companyId && p.cod_proveedor == cod && (p.status == null || p.status == true), ct);
        if (!existe)
        {
            throw new InvalidOperationException("El proveedor seleccionado no existe o está inactivo.");
        }
    }

    /// <summary>
    /// Cotas de la cabecera. El descuento es PORCENTAJE: fuera de 0–100 el total saldría negativo.
    /// <para>
    /// La fecha de entrega pactada es OBLIGATORIA desde el borrador (decisión usuario
    /// 2026-08-14): es lo único contra lo que se puede medir la puntualidad del proveedor.
    /// La columna admite NULL en la BD sólo por las órdenes anteriores a ese cambio.
    /// </para>
    /// </summary>
    private static void ValidarCabecera(OrdenCompraDto dto, DateOnly fechaOrden)
    {
        if (dto.Descuento < 0m || dto.Descuento > 100m)
        {
            throw new InvalidOperationException("El descuento debe estar entre 0 y 100 por ciento.");
        }
        if (dto.OtrosGastos < 0m)
        {
            throw new InvalidOperationException("Los otros gastos no pueden ser negativos.");
        }
        ExigirMonto(dto.OtrosGastos, "Los otros gastos");

        if (dto.FechaEntregaPactada is null)
        {
            throw new InvalidOperationException("Indique la fecha de entrega pactada con el proveedor.");
        }
        ExigirFechaPactada(dto.FechaEntregaPactada.Value, fechaOrden, "La fecha de entrega pactada");
    }

    /// <summary>Años máximos entre la fecha de la orden y la entrega pactada (atajo contra el dedazo de año).</summary>
    private const int MaxAniosEntrega = 5;

    /// <summary>
    /// Acota una fecha pactada: no puede quedar antes de la orden (sería una entrega ya vencida
    /// al nacer) ni a más de <see cref="MaxAniosEntrega"/> años (típico error de tecleo del año,
    /// que ensuciaría el indicador de puntualidad sin que nadie lo note).
    /// </summary>
    private static void ExigirFechaPactada(DateOnly valor, DateOnly fechaOrden, string que)
    {
        if (valor < fechaOrden)
        {
            throw new InvalidOperationException($"{que} no puede ser anterior a la fecha de la orden.");
        }
        if (valor > fechaOrden.AddYears(MaxAniosEntrega))
        {
            throw new InvalidOperationException($"{que} está demasiado lejos: revise el año.");
        }
    }

    /// <summary>
    /// Regla dura del usuario (2026-07-30): sólo se puede pedir un artículo a un proveedor si
    /// el artículo existe en almacén CON su código de proveedor. Es decir, exige una fila
    /// ACTIVA en <c>alm_articulo_proveedor</c> para el par y con <c>codigo_upc</c> con valor
    /// — una relación activa sin código NO habilita la compra.
    /// </summary>
    /// <returns>Mapa artículo → código de proveedor, usado como snapshot del renglón.</returns>
    private async Task<IReadOnlyDictionary<int, string>> ValidarRenglonesAsync(
        IReadOnlyCollection<OrdenCompraDetalleDto> detalles, string cod, DateOnly fechaOrden, CancellationToken ct)
    {
        if (detalles is null || detalles.Count == 0)
        {
            throw new InvalidOperationException("La orden debe tener al menos un renglón.");
        }
        foreach (var d in detalles)
        {
            if (d.ArticuloId <= 0)
            {
                throw new InvalidOperationException("Hay un renglón sin artículo.");
            }
            if (d.CantidadPedida <= 0)
            {
                throw new InvalidOperationException("La cantidad pedida debe ser mayor que cero.");
            }
            if (d.CostoUnitario <= 0)
            {
                throw new InvalidOperationException("El costo unitario debe ser mayor que cero.");
            }
            // El ISV del renglón no se valida aquí: lo calcula el servidor desde la tasa del tipo
            // (ver AplicarRenglonesYTotales); el impuesto que venga en el DTO se ignora.
            ExigirCantidad(d.CantidadPedida, "La cantidad pedida");
            ExigirCantidad(d.CostoUnitario, "El costo unitario");

            // La fecha por renglón es OPCIONAL (sólo entregas escalonadas): vacía = rige la de
            // la cabecera. Si viene, se acota igual que aquélla.
            if (d.FechaEntregaPactada is not null)
            {
                ExigirFechaPactada(d.FechaEntregaPactada.Value, fechaOrden, "La fecha de entrega del renglón");
            }
        }

        var articuloIds = detalles.Select(d => d.ArticuloId).Distinct().ToList();
        var conCodigo = await _context.alm_articulo_proveedors.AsNoTracking()
            .Where(p => p.cod_proveedor == cod
                     && p.activo
                     && p.codigo_upc != null
                     && p.codigo_upc != ""
                     && articuloIds.Contains(p.articulo_id))
            .Select(p => new { p.articulo_id, p.codigo_upc })
            .ToListAsync(ct);

        var mapa = conCodigo
            .GroupBy(x => x.articulo_id)
            .ToDictionary(g => g.Key, g => g.First().codigo_upc!);

        var faltantes = articuloIds.Except(mapa.Keys).ToList();
        if (faltantes.Count > 0)
        {
            throw new InvalidOperationException(
                $"Hay {faltantes.Count} artículo(s) sin código para el proveedor {cod}. Regístralo antes de comprarlo.");
        }

        return mapa;
    }

    /// <summary>Tope de <c>NUMERIC(14,2)</c> (12 dígitos enteros), exclusivo.</summary>
    private const decimal MaxMonto = 1_000_000_000_000m;

    /// <summary>Tope de <c>NUMERIC(14,4)</c> (10 dígitos enteros), exclusivo.</summary>
    private const decimal MaxCantidad = 10_000_000_000m;

    // Sin estas guardas el valor llega a Postgres, revienta con 'numeric field overflow'
    // envuelto en DbUpdateException y el cliente ve un 500 en vez de un 400 con mensaje.
    private static void ExigirMonto(decimal valor, string que)
    {
        if (Math.Abs(valor) >= MaxMonto)
        {
            throw new InvalidOperationException($"{que} está fuera del rango permitido.");
        }
    }

    private static void ExigirCantidad(decimal valor, string que)
    {
        if (Math.Abs(valor) >= MaxCantidad)
        {
            throw new InvalidOperationException($"{que} está fuera del rango permitido.");
        }
    }

    private async Task ResolverProveedoresAsync(IEnumerable<string> codigos, Action<string, string?> asignar, CancellationToken ct)
    {
        var codes = codigos.Where(c => !string.IsNullOrWhiteSpace(c)).Distinct().ToList();
        if (codes.Count == 0) return;

        var companyId = _company.GetCompanyId();
        var nombres = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == companyId && codes.Contains(p.cod_proveedor))
            .Select(p => new { p.cod_proveedor, p.nombre })
            .ToListAsync(ct);

        foreach (var n in nombres.GroupBy(x => x.cod_proveedor))
        {
            asignar(n.Key, n.First().nombre);
        }
    }

    private static string NormalizarCodProveedor(string? cod)
    {
        var trimmed = (cod ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new InvalidOperationException("Debe seleccionar un proveedor.");
        }
        if (trimmed.Length > 20)
        {
            throw new InvalidOperationException("El código de proveedor supera 20 caracteres.");
        }
        return trimmed;
    }

    private static decimal Redondear2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
