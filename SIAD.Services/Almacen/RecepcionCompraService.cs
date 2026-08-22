using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.DTOs.Mantenimientos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Recepción de compras. Ver <see cref="IRecepcionCompraService"/>.
/// <para>
/// El alta hace cuatro cosas en UNA transacción: crea la cabecera con su correlativo, crea
/// una línea de <c>alm_compra</c> por renglón, descarga la O/C si la hay, y postea cada línea
/// al kardex con el motor. Si algo falla, no queda ni media factura ni medio asiento.
/// </para>
/// </summary>
public sealed class RecepcionCompraService : IRecepcionCompraService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IInventarioPostingService _motor;
    private readonly ITasaIsvArticuloResolver _tasas;

    public RecepcionCompraService(
        SiadDbContext context, ICurrentCompanyService company, IInventarioPostingService motor,
        ITasaIsvArticuloResolver tasas)
    {
        _context = context;
        _company = company;
        _motor = motor;
        _tasas = tasas;
    }

    // ── Consultas ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RecepcionCompraListItemDto>> GetAsync(
        RecepcionCompraFilterDto? filtro, CancellationToken ct = default)
    {
        filtro ??= new RecepcionCompraFilterDto();

        var query = _context.alm_compra_hdrs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.CodProveedor))
        {
            var cod = filtro.CodProveedor.Trim();
            query = query.Where(h => h.cod_proveedor == cod);
        }
        if (filtro.Estado.HasValue)
        {
            query = query.Where(h => h.estado == filtro.Estado.Value);
        }
        if (filtro.OrdenCompraId.HasValue)
        {
            query = query.Where(h => h.orden_compra_id == filtro.OrdenCompraId.Value);
        }
        if (filtro.FechaDesde.HasValue)
        {
            query = query.Where(h => h.fecha >= filtro.FechaDesde.Value);
        }
        if (filtro.FechaHasta.HasValue)
        {
            query = query.Where(h => h.fecha <= filtro.FechaHasta.Value);
        }
        if (!string.IsNullOrWhiteSpace(filtro.Search))
        {
            var termino = filtro.Search.Trim();
            var like = $"%{termino}%";
            query = query.Where(h =>
                EF.Functions.ILike(h.cod_proveedor, like) ||
                EF.Functions.ILike(h.proveedor ?? string.Empty, like) ||
                EF.Functions.ILike(h.numero_factura_sar ?? string.Empty, like) ||
                EF.Functions.ILike(h.observaciones ?? string.Empty, like) ||
                h.numero.ToString().Contains(termino));
        }

        var filas = await query
            .OrderByDescending(h => h.fecha)
            .ThenByDescending(h => h.numero)
            .Select(h => new RecepcionCompraListItemDto
            {
                Id = h.id,
                Numero = h.numero,
                Fecha = h.fecha,
                CodProveedor = h.cod_proveedor,
                ProveedorNombre = h.proveedor,
                NumeroFacturaSar = h.numero_factura_sar,
                OrdenCompraNumero = h.orden == null ? null : h.orden.numero,
                BodegaNombre = h.bodega_ref == null ? null : h.bodega_ref.nombre,
                Total = h.total,
                Estado = h.estado,
                Posteado = h.posteado,
                Renglones = h.lineas.Count
            })
            .ToListAsync(ct);

        return filas;
    }

    public async Task<RecepcionCompraDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var h = await _context.alm_compra_hdrs.AsNoTracking()
            .Include(x => x.lineas)
            .Include(x => x.orden)
            .Include(x => x.bodega_ref)
            .FirstOrDefaultAsync(x => x.id == id, ct);
        if (h is null) return null;

        var dto = MapCabecera(h);

        var articuloIds = h.lineas.Where(l => l.articulo_id != null)
            .Select(l => l.articulo_id!.Value).Distinct().ToList();
        var upcs = await _context.alm_articulo_proveedors.AsNoTracking()
            .Where(p => p.cod_proveedor == h.cod_proveedor && articuloIds.Contains(p.articulo_id))
            .Select(p => new { p.articulo_id, p.codigo_upc })
            .ToListAsync(ct);
        var mapaUpc = upcs.GroupBy(x => x.articulo_id)
            .ToDictionary(g => g.Key, g => g.First().codigo_upc);

        // El costo con que la línea entró al inventario NO se guarda en alm_compra (ahí vive
        // el costo facturado): la verdad es el valor_unitario del asiento, que ya trae el ISV
        // capitalizado o no según la política que se aplicó al postear.
        var lineaIds = h.lineas.Select(l => l.id).ToList();
        var costosPosteados = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.documento_tipo == TipoDocumentoInventario.Compra
                     && k.documento_id != null
                     && lineaIds.Contains(k.documento_id.Value))
            .Select(k => new { LineaId = k.documento_id!.Value, k.valor_unitario })
            .ToListAsync(ct);
        var mapaCosto = costosPosteados
            .GroupBy(x => x.LineaId)
            .ToDictionary(g => g.Key, g => g.First().valor_unitario);

        dto.Detalles = h.lineas
            .OrderBy(l => l.id)
            .Select(l => new RecepcionCompraDetalleDto
            {
                Id = l.id,
                ArticuloId = l.articulo_id ?? 0,
                CodigoArticulo = l.codigo_articulo,
                Descripcion = l.concepto,
                CodigoUpc = l.articulo_id != null && mapaUpc.TryGetValue(l.articulo_id.Value, out var upc) ? upc : null,
                OrdenDetalleId = l.orden_compra_detalle_id,
                Cantidad = l.cantidad,
                CostoUnitario = l.precio_unitario,
                Impuesto = l.impuesto,
                Descuento = l.descuento,
                Total = l.total,
                CostoPosteado = mapaCosto.TryGetValue(l.id, out var costo) ? costo : 0m
            })
            .ToList();

        return dto;
    }

    public async Task<IReadOnlyList<OrdenCompraPendienteDto>> ObtenerPendientesOrdenAsync(
        int ordenCompraId, CancellationToken ct = default)
    {
        var orden = await _context.alm_orden_compras.AsNoTracking()
            .FirstOrDefaultAsync(o => o.id == ordenCompraId, ct);
        if (orden is null || !EsRecibible(orden.estado))
        {
            return Array.Empty<OrdenCompraPendienteDto>();
        }

        var renglones = await (
            from d in _context.alm_orden_compra_detalles.AsNoTracking()
            join a in _context.alm_articulos.AsNoTracking() on d.articulo_id equals a.id
            where d.orden_compra_id == ordenCompraId
               && d.cantidad_pedida > d.cantidad_aplicada
            orderby d.id
            select new OrdenCompraPendienteDto
            {
                OrdenDetalleId = d.id,
                ArticuloId = d.articulo_id,
                CodigoArticulo = a.codigo_articulo,
                Descripcion = d.descripcion ?? a.descripcion,
                CodigoUpc = d.codigo_upc,
                CantidadPedida = d.cantidad_pedida,
                CantidadAplicada = d.cantidad_aplicada,
                CantidadPendiente = d.cantidad_pedida - d.cantidad_aplicada,
                CostoUnitario = d.costo_unitario
            })
            .ToListAsync(ct);

        return renglones;
    }

    public async Task<IReadOnlyList<OrdenCompraListItemDto>> ObtenerOrdenesRecibiblesAsync(
        string? codProveedor, CancellationToken ct = default)
    {
        var query = _context.alm_orden_compras.AsNoTracking()
            .Where(o => o.estado == EstadoOrdenCompra.Aprobada
                     || o.estado == EstadoOrdenCompra.RecibidaParcial);

        if (!string.IsNullOrWhiteSpace(codProveedor))
        {
            var cod = codProveedor.Trim();
            query = query.Where(o => o.cod_proveedor == cod);
        }

        var filas = await query
            .OrderByDescending(o => o.numero)
            .Select(o => new OrdenCompraListItemDto
            {
                Id = o.id,
                Numero = o.numero,
                Fecha = o.fecha,
                CodProveedor = o.cod_proveedor,
                Total = o.total,
                Estado = o.estado,
                Renglones = o.detalles.Count
            })
            .Take(200)
            .ToListAsync(ct);

        // El nombre del proveedor hace legible el combo cuando se listan las órdenes de todos
        // los proveedores (sin uno ya elegido), no sólo las de uno.
        await ResolverProveedoresAsync(filas.Select(f => f.CodProveedor),
            (cod, nombre) => { foreach (var f in filas.Where(x => x.CodProveedor == cod)) f.ProveedorNombre = nombre; }, ct);

        return filas;
    }

    public async Task<RecepcionCompraImpresionDto?> GetDatosImpresionAsync(int id, string impresoPor, CancellationToken ct = default)
    {
        var doc = await GetByIdAsync(id, ct);
        if (doc is null) return null;

        // El No. de factura y el CAI se guardan normalizados: se imprimen con la máscara
        // configurada. El reporte no consulta el catálogo, recibe el texto ya listo.
        await AplicarFormatosFiscalesAsync(doc, ct);

        var empresa = await CargarEmpresaAsync(ct);
        return new RecepcionCompraImpresionDto
        {
            EmpresaNombre = empresa?.commercial_name ?? string.Empty,
            EmpresaRazonSocial = empresa?.legal_name,
            EmpresaRtn = empresa?.tax_id,
            EmpresaDireccion = empresa?.address,
            EmpresaTelefono = empresa?.phone,
            EmpresaEmail = empresa?.email,
            EmpresaLogo = empresa?.logo,
            ImpresoPor = ClasificacionNormalizer.Usuario(impresoPor),
            Documento = doc,
            MontoEnLetras = doc.Total > 0m ? NumerosALetras.Convertir(doc.Total) : string.Empty
        };
    }

    private async Task<cfg_company?> CargarEmpresaAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        return await _context.cfg_companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.company_id == companyId, ct);
    }

    /// <summary>
    /// Contrasta el No. de factura del proveedor y el CAI contra el catálogo de formatos fiscales
    /// y los deja normalizados como se van a guardar. Sin formato configurado (o inactivo) no toca
    /// nada: el campo sigue siendo texto libre, igual que antes del catálogo.
    /// </summary>
    /// <remarks>
    /// La pantalla ya valida y avisa; esto es la última palabra, porque el POST puede venir de
    /// cualquier lado y porque lo que se guarda tiene que quedar en una sola forma canónica —
    /// si no, el índice antiduplicado uq_alm_compra_hdr_factura_prov compara peras con manzanas.
    /// </remarks>
    private async Task ValidarYNormalizarCodigosFiscalesAsync(RecepcionCompraDto dto, CancellationToken ct)
    {
        var formatos = await _context.cfg_formato_fiscals.AsNoTracking()
            .Where(f => f.activo)
            .Select(f => new
            {
                f.codigo,
                f.nombre,
                f.mascara,
                f.patron,
                f.modo_validacion,
                f.obligatorio,
                f.normalizar,
                f.mayusculas
            })
            .ToListAsync(ct);

        if (formatos.Count == 0) return;

        foreach (var f in formatos)
        {
            var esSar = string.Equals(f.codigo, CodigoFormatoNumeroSar, StringComparison.OrdinalIgnoreCase);
            var esCai = string.Equals(f.codigo, CodigoFormatoCai, StringComparison.OrdinalIgnoreCase);
            if (!esSar && !esCai) continue;

            var valor = esSar ? dto.NumeroFacturaSar : dto.Cai;

            if (string.IsNullOrWhiteSpace(valor))
            {
                if (f.obligatorio)
                {
                    throw new InvalidOperationException($"Debe indicar el {f.nombre}.");
                }
                continue;
            }

            if (f.modo_validacion == ModoValidacionFormatoFiscal.Bloquea
                && !FiscalCodeFormatter.EsValido(valor, f.mascara, f.patron, f.mayusculas))
            {
                throw new InvalidOperationException(
                    $"El {f.nombre} debe tener el formato {FiscalCodeFormatter.Ejemplo(f.mascara)}.");
            }

            var guardado = f.normalizar
                ? FiscalCodeFormatter.Normalizar(valor, f.mayusculas)
                : (f.mayusculas ? valor.Trim().ToUpperInvariant() : valor.Trim());

            if (esSar) dto.NumeroFacturaSar = guardado;
            else dto.Cai = guardado;
        }
    }

    /// <summary>
    /// Pone la máscara del catálogo de formatos fiscales al No. de factura del proveedor y al CAI.
    /// Sin formato configurado (o inactivo) los deja como están: es lo que se imprimía antes.
    /// </summary>
    private async Task AplicarFormatosFiscalesAsync(RecepcionCompraDto doc, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(doc.NumeroFacturaSar) && string.IsNullOrWhiteSpace(doc.Cai))
        {
            return;
        }

        var formatos = await _context.cfg_formato_fiscals.AsNoTracking()
            .Where(f => f.activo)
            .Select(f => new { f.codigo, f.mascara, f.mayusculas })
            .ToListAsync(ct);

        foreach (var f in formatos)
        {
            if (string.Equals(f.codigo, CodigoFormatoNumeroSar, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(doc.NumeroFacturaSar))
            {
                doc.NumeroFacturaSar = FiscalCodeFormatter.Formatear(doc.NumeroFacturaSar, f.mascara, f.mayusculas);
            }
            else if (string.Equals(f.codigo, CodigoFormatoCai, StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(doc.Cai))
            {
                doc.Cai = FiscalCodeFormatter.Formatear(doc.Cai, f.mascara, f.mayusculas);
            }
        }
    }

    private const string CodigoFormatoNumeroSar = "NUMERO_SAR";
    private const string CodigoFormatoCai = "CAI";

    // ── Alta ─────────────────────────────────────────────────────────────────

    public async Task<RecepcionCompraDto> CrearAsync(
        RecepcionCompraDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        // Idempotencia del alta: el mismo documento reenviado NO se registra dos veces.
        // Se comprueba antes de tocar nada; el índice único sobre (company_id, uuid) es la
        // red de seguridad si dos peticiones entran a la vez.
        if (dto.Uuid.HasValue)
        {
            var yaExiste = await _context.alm_compra_hdrs.AsNoTracking()
                .FirstOrDefaultAsync(h => h.uuid == dto.Uuid.Value, ct);
            if (yaExiste is not null)
            {
                return (await GetByIdAsync(yaExiste.id, ct))!;
            }
        }

        var cod = NormalizarCodProveedor(dto.CodProveedor);
        ValidarCabecera(dto);
        await ValidarYNormalizarCodigosFiscalesAsync(dto, ct);
        await ValidarProveedorAsync(cod, companyId, ct);
        var bodega = await ValidarBodegaAsync(dto.BodegaId, ct);
        var upcs = await ValidarRenglonesAsync(dto.Detalles, cod, ct);

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var fecha = dto.Fecha ?? DateOnly.FromDateTime(DateTime.Today);

        // Política del ISV: capa 1 (tasa por tipo de artículo) y capa 2 (qué hace la empresa
        // con ese ISV), más el override de la factura.
        var tasas = await _tasas.ResolverAsync(dto.Detalles.Select(d => d.ArticuloId).ToList(), fecha, ct);

        // El código del artículo se toma del CATÁLOGO, no del DTO: es un snapshot y el
        // cliente puede no mandarlo (o mandar cualquier cosa). Mismo criterio que el
        // codigo_upc del proveedor.
        var codigos = await CodigosArticuloAsync(dto.Detalles.Select(d => d.ArticuloId).Distinct().ToList(), ct);
        var capitalizaIsv = await ResolverSiCapitalizaIsvAsync(dto.DetallarIsv, ct);

        // TransaccionAmbiente, no BeginTransactionAsync: los tests envuelven cada prueba en
        // BEGIN … ROLLBACK y anidar no está soportado.
        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // La O/C se bloquea ANTES de validar el tope: una orden admite varias facturas, así
        // que dos recepciones simultáneas de la misma orden podrían leer el mismo pendiente
        // y recibir de más entre las dos. El candado las serializa.
        var (orden, pendientes) = await BloquearYLeerPendientesAsync(dto, cod, ct);

        var numero = await SiguienteNumeroAsync(companyId, ct);

        var cabecera = new alm_compra_hdr
        {
            numero = numero,
            fecha = fecha,
            fecha_factura = dto.FechaFactura,
            fecha_vencimiento = dto.FechaVencimiento,
            cod_proveedor = cod,
            proveedor = await NombreProveedorAsync(cod, companyId, ct),
            numero_factura_sar = ClasificacionNormalizer.Opcional(dto.NumeroFacturaSar, 30),
            cai = ClasificacionNormalizer.Opcional(dto.Cai, 50),
            orden_compra_id = dto.OrdenCompraId,
            bodega_id = bodega.id,
            terminos_pago = ClasificacionNormalizer.Opcional(dto.TerminosPago, 100),
            tipo_compra = dto.TipoCompra,
            plazo_dias = dto.PlazoDias,
            moneda = dto.Moneda,
            tasa_cambio = dto.TasaCambio,
            consumo_interno = dto.ConsumoInterno,
            detallar_isv = dto.DetallarIsv,
            descuento = dto.Descuento,
            otros_gastos = dto.OtrosGastos,
            flete_seguro = dto.FleteSeguro,
            observaciones = ClasificacionNormalizer.Opcional(dto.Observaciones, 1000),
            estado = EstadoRecepcionCompra.Registrada,
            posteado = false,
            uuid = dto.Uuid ?? Guid.NewGuid(),
            usuariocreacion = usuario,
            fechacreacion = ahora
        };

        // Término de pago del catálogo: si se eligió uno, manda sobre el texto libre y define
        // el vencimiento por sus días.
        await AplicarTerminoPagoAsync(cabecera, dto, fecha, ct);

        var lineas = ArmarLineasYTotales(cabecera, dto, upcs, codigos, tasas, capitalizaIsv);

        _context.alm_compra_hdrs.Add(cabecera);
        foreach (var (linea, _) in lineas)
        {
            _context.alm_compras.Add(linea);
        }

        // Un solo SaveChanges: las líneas necesitan su id (es el documento del asiento) y la
        // cabecera su FK, así que ambas cosas se materializan aquí.
        await _context.SaveChangesAsync(ct);

        // Descarga de la O/C. Acumulativa: una orden puede recibirse en varias facturas.
        AplicarDescargaOrden(dto, orden, pendientes);

        // Posteo al kardex, una línea a la vez. El motor vuelve a bloquear la fila del par y
        // es idempotente por uuid: si esta transacción se reintenta, no duplica asientos.
        foreach (var (linea, costoPosteo) in lineas)
        {
            var filaPar = await ResolverParAsync(linea.articulo_id!.Value, cabecera.bodega_id, usuario, ahora, ct);

            await _motor.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.Compra,
                ArticuloBodegaId = filaPar,
                Cantidad = linea.cantidad,
                CostoUnitario = costoPosteo,
                Fecha = fecha,
                DocumentoTipo = TipoDocumentoInventario.Compra,
                DocumentoId = linea.id,
                Observacion = $"Compra {numero:00000} · proveedor {cod}"
            }, user, ct);

            linea.posteado = true;
            linea.fecha_posteo = ahora;
        }

        cabecera.posteado = true;
        cabecera.fecha_posteo = ahora;

        // Toda factura genera su cuenta por pagar (contado o crédito); todas se pagan desde la
        // vista unificada.
        GenerarCxp(cabecera);

        // Fase 2: asiento contable de la compra (DEBE inventario / HABER proveedor), módulo COMPRAS,
        // gated por activo_compras (Compras es su propio módulo contable). No-op si el módulo COMPRAS
        // está inactivo (el comportamiento de hoy).
        await CompraContabilidad.ContabilizarFacturaAsync(
            _context, companyId, cod, cabecera.total,
            lineas.Select(l => (l.Linea.articulo_id!.Value, l.CostoPosteo * l.Linea.cantidad)).ToList(),
            cabecera.id, cabecera.numero.ToString("00000"), fecha,
            $"Compra {cabecera.numero:00000} · proveedor {cod}", usuario, ct);

        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return (await GetByIdAsync(cabecera.id, ct))!;
    }

    /// <summary>
    /// Crea la cuenta por pagar de la factura: saldo = total, estado Pendiente, vencimiento del
    /// término (o la fecha de la factura si no hay). El índice único (company_id, compra_hdr_id)
    /// garantiza una sola CxP por factura. Prepagado no genera CxP (ya está pagada).
    /// </summary>
    private void GenerarCxp(alm_compra_hdr cabecera)
    {
        if (cabecera.condicion_pago == CondicionPagoCompra.Prepagado) return;

        _context.alm_compra_cxps.Add(new alm_compra_cxp
        {
            compra_hdr_id = cabecera.id,
            cod_proveedor = cabecera.cod_proveedor,
            proveedor = cabecera.proveedor,
            numero_factura_sar = cabecera.numero_factura_sar,
            fecha = cabecera.fecha,
            fecha_vencimiento = cabecera.fecha_vencimiento ?? cabecera.fecha,
            condicion_pago = cabecera.condicion_pago,
            monto = cabecera.total,
            saldo = cabecera.total,
            estado_id = EstadoCompraCxp.Pendiente,
            usuariocreacion = cabecera.usuariocreacion,
            fechacreacion = cabecera.fechacreacion
        });
    }

    // ── Anulación ────────────────────────────────────────────────────────────

    public async Task<bool> AnularAsync(int id, string? motivo, string user, CancellationToken ct = default)
    {
        var companyId = _company.GetCompanyId();
        if (companyId <= 0) throw new InvalidOperationException("No se pudo resolver la empresa actual.");

        var cabecera = await _context.alm_compra_hdrs
            .Include(h => h.lineas)
            .FirstOrDefaultAsync(h => h.id == id, ct);
        if (cabecera is null) return false;

        // Idempotente: anular dos veces no revierte dos veces.
        if (cabecera.estado == EstadoRecepcionCompra.Anulada) return true;

        var usuario = ClasificacionNormalizer.Usuario(user);
        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // Asientos de compra de estas líneas: son los que hay que revertir.
        var lineaIds = cabecera.lineas.Select(l => l.id).ToList();
        var asientos = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.documento_tipo == TipoDocumentoInventario.Compra
                     && k.documento_id != null
                     && lineaIds.Contains(k.documento_id.Value))
            .Select(k => new { k.id, LineaId = k.documento_id!.Value, k.articulo_id, k.bodega_id, k.cantidad, k.valor_unitario })
            .ToListAsync(ct);

        var porLinea = asientos.GroupBy(a => a.LineaId).ToDictionary(g => g.Key, g => g.First());

        // Antes de tocar nada: la mercadería tiene que seguir estando. Si ya salió, la
        // reversa dejaría la existencia en negativo y eso no se arregla anulando.
        foreach (var linea in cabecera.lineas)
        {
            if (!porLinea.TryGetValue(linea.id, out var asiento)) continue;

            var fila = await _context.alm_articulo_bodegas.AsNoTracking()
                .FirstOrDefaultAsync(u => u.articulo_id == asiento.articulo_id && u.bodega_id == asiento.bodega_id, ct);

            var disponible = fila?.existencia ?? 0m;
            if (disponible < asiento.cantidad)
            {
                throw new InvalidOperationException(
                    $"No se puede anular: del artículo {linea.codigo_articulo ?? asiento.articulo_id?.ToString()} entraron " +
                    $"{asiento.cantidad:0.####} y sólo quedan {disponible:0.####} en la bodega. " +
                    "La mercadería ya salió; corrija con un ajuste de inventario.");
            }
        }

        // Devolver lo recibido a la O/C, bajo candado (otra recepción de la misma orden
        // podría estar corriendo en paralelo).
        await DevolverALaOrdenAsync(cabecera, companyId, ct);

        // Contra-asiento por línea. El uuid de la reversa se deriva del asiento revertido, así
        // que reintentar la anulación no duplica reversas.
        foreach (var linea in cabecera.lineas)
        {
            if (!porLinea.TryGetValue(linea.id, out var asiento)) continue;

            var fila = await _context.alm_articulo_bodegas.AsNoTracking()
                .FirstAsync(u => u.articulo_id == asiento.articulo_id && u.bodega_id == asiento.bodega_id, ct);

            await _motor.PostearAsync(new MovimientoInventarioDto
            {
                Tipo = TipoMovimientoInventario.Reversa,
                ArticuloBodegaId = fila.id,
                Cantidad = asiento.cantidad,
                CostoUnitario = asiento.valor_unitario,
                Fecha = DateOnly.FromDateTime(DateTime.Today),
                KardexIdRevertido = asiento.id,
                Observacion = $"Anulación de la compra {cabecera.numero:00000}"
            }, user, ct);
        }

        // Anula la cuenta por pagar de la factura (rechaza si ya tiene pagos aplicados).
        await AnularCxpDeFacturaAsync(cabecera, usuario, ahora, ct);

        // Fase 2: revierte el asiento contable de la compra (no-op si el flag estaba apagado al crearla).
        await CompraContabilidad.RevertirFacturaAsync(_context, companyId, cabecera.id, usuario, ct);

        cabecera.estado = EstadoRecepcionCompra.Anulada;
        cabecera.observaciones = AnexarMotivo(cabecera.observaciones, motivo, usuario);
        cabecera.usuariomodificacion = usuario;
        cabecera.fechamodificacion = ahora;

        await _context.SaveChangesAsync(ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    /// <summary>
    /// Anula la cuenta por pagar generada por la factura. Si tiene abonos vigentes, rechaza: los
    /// pagos deben anularse antes (no se deshace una factura ya pagada por esta vía).
    /// </summary>
    private async Task AnularCxpDeFacturaAsync(alm_compra_hdr cabecera, string usuario, DateTime ahora, CancellationToken ct)
    {
        var cxp = await _context.alm_compra_cxps.FirstOrDefaultAsync(c => c.compra_hdr_id == cabecera.id, ct);
        if (cxp is null || cxp.estado_id == EstadoCompraCxp.Anulada) return;

        var tienePagos = await _context.alm_compra_cxp_abonos
            .AnyAsync(a => a.cxp_id == cxp.id && a.estado == "V", ct);
        if (tienePagos)
        {
            throw new InvalidOperationException(
                "No se puede anular la factura: su cuenta por pagar tiene pagos aplicados. Anule primero los pagos.");
        }

        cxp.estado_id = EstadoCompraCxp.Anulada;
        cxp.saldo = 0m;
        cxp.usuariomodificacion = usuario;
        cxp.fechamodificacion = ahora;
    }

    /// <summary>
    /// Resta de <c>cantidad_aplicada</c> lo que esta factura había recibido y recalcula el
    /// estado de la orden: vuelve a Aprobada si no queda nada aplicado, o a Recibida parcial
    /// si otras facturas siguen vigentes.
    /// </summary>
    private async Task DevolverALaOrdenAsync(alm_compra_hdr cabecera, long companyId, CancellationToken ct)
    {
        if (!cabecera.orden_compra_id.HasValue) return;

        var ordenId = cabecera.orden_compra_id.Value;
        var orden = await _context.alm_orden_compras.FirstOrDefaultAsync(o => o.id == ordenId, ct);
        if (orden is null) return;

        var renglones = await _context.alm_orden_compra_detalles
            .FromSqlInterpolated($@"
                SELECT * FROM alm_orden_compra_detalle
                 WHERE company_id = {companyId} AND orden_compra_id = {ordenId}
                 ORDER BY id
                 FOR UPDATE")
            .ToListAsync(ct);

        var porId = renglones.ToDictionary(r => r.id);

        foreach (var linea in cabecera.lineas.Where(l => l.orden_compra_detalle_id.HasValue))
        {
            if (!porId.TryGetValue(linea.orden_compra_detalle_id!.Value, out var renglon)) continue;

            // Max(0) por si un dato viejo dejara la resta en negativo: el CHECK de la BD lo
            // rechazaría y el mensaje sería incomprensible.
            renglon.cantidad_aplicada = Math.Max(0m, renglon.cantidad_aplicada - linea.cantidad);
        }

        var algoAplicado = renglones.Exists(r => r.cantidad_aplicada > 0m);
        var todoCubierto = renglones.TrueForAll(r => r.cantidad_aplicada >= r.cantidad_pedida);

        orden.estado = todoCubierto ? EstadoOrdenCompra.Cerrada
            : algoAplicado ? EstadoOrdenCompra.RecibidaParcial
            : EstadoOrdenCompra.Aprobada;
        orden.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Deja constancia del motivo en las observaciones. No hay columna propia: la traza de
    /// quién y cuándo va en usuariomodificacion/fechamodificacion.
    /// </summary>
    private static string? AnexarMotivo(string? observaciones, string? motivo, string usuario)
    {
        var texto = string.IsNullOrWhiteSpace(motivo)
            ? $"[ANULADA por {usuario}]"
            : $"[ANULADA por {usuario}: {motivo.Trim()}]";

        var resultado = string.IsNullOrWhiteSpace(observaciones)
            ? texto
            : $"{observaciones}\n{texto}";

        return resultado.Length > 1000 ? resultado[..1000] : resultado;
    }

    // ── Piezas internas ──────────────────────────────────────────────────────

    private static bool EsRecibible(short estadoOrden) =>
        estadoOrden is EstadoOrdenCompra.Aprobada or EstadoOrdenCompra.RecibidaParcial;

    /// <summary>
    /// Bloquea los renglones de la O/C y devuelve el mapa renglón → entidad, ya validado
    /// contra lo que se pretende recibir. En compra directa devuelve un mapa vacío.
    /// </summary>
    private async Task<(alm_orden_compra? Orden, Dictionary<int, alm_orden_compra_detalle> Renglones)>
        BloquearYLeerPendientesAsync(RecepcionCompraDto dto, string cod, CancellationToken ct)
    {
        var vacio = new Dictionary<int, alm_orden_compra_detalle>();

        if (!dto.OrdenCompraId.HasValue)
        {
            // Compra directa: ningún renglón puede pretender descargar una O/C.
            if (dto.Detalles.Any(d => d.OrdenDetalleId.HasValue))
            {
                throw new InvalidOperationException(
                    "Hay renglones que apuntan a una orden de compra, pero la factura no indica cuál.");
            }
            return (null, vacio);
        }

        var ordenId = dto.OrdenCompraId.Value;
        var orden = await _context.alm_orden_compras.FirstOrDefaultAsync(o => o.id == ordenId, ct)
            ?? throw new InvalidOperationException("La orden de compra no existe.");

        if (orden.cod_proveedor != cod)
        {
            throw new InvalidOperationException(
                "La orden de compra es de otro proveedor que el de la factura.");
        }
        if (!EsRecibible(orden.estado))
        {
            throw new InvalidOperationException(orden.estado switch
            {
                EstadoOrdenCompra.Borrador => "La orden de compra todavía no está aprobada.",
                EstadoOrdenCompra.Cerrada => "La orden de compra ya fue recibida por completo.",
                EstadoOrdenCompra.Anulada => "La orden de compra está anulada.",
                _ => "La orden de compra no admite recepciones."
            });
        }

        var companyId = _company.GetCompanyId();

        // FOR UPDATE con el company_id DENTRO del SQL crudo: EF compone su filtro de tenant
        // por encima de la consulta, así que sin esto el candado se tomaría antes de filtrar.
        var renglones = await _context.alm_orden_compra_detalles
            .FromSqlInterpolated($@"
                SELECT * FROM alm_orden_compra_detalle
                 WHERE company_id = {companyId} AND orden_compra_id = {ordenId}
                 ORDER BY id
                 FOR UPDATE")
            .ToListAsync(ct);

        var mapa = renglones.ToDictionary(r => r.id);

        foreach (var d in dto.Detalles)
        {
            if (!d.OrdenDetalleId.HasValue)
            {
                throw new InvalidOperationException(
                    "En una recepción contra orden de compra, cada renglón debe indicar qué renglón de la orden descarga.");
            }
            if (!mapa.TryGetValue(d.OrdenDetalleId.Value, out var renglon))
            {
                throw new InvalidOperationException("Hay un renglón que no pertenece a esta orden de compra.");
            }
            if (renglon.articulo_id != d.ArticuloId)
            {
                throw new InvalidOperationException(
                    "Un renglón recibe un artículo distinto al que pide la orden de compra.");
            }

            var pendiente = renglon.cantidad_pedida - renglon.cantidad_aplicada;
            if (d.Cantidad > pendiente)
            {
                throw new InvalidOperationException(
                    $"El renglón del artículo {d.ArticuloId} recibe {d.Cantidad:0.####} y solo quedan {pendiente:0.####} pendientes en la orden.");
            }
        }

        // Dos renglones de la misma factura no pueden descargar el mismo renglón de la O/C:
        // por separado pasan el tope y juntos lo rompen.
        var repetidos = dto.Detalles
            .GroupBy(d => d.OrdenDetalleId!.Value)
            .Where(g => g.Count() > 1)
            .ToList();
        if (repetidos.Count > 0)
        {
            throw new InvalidOperationException(
                "Hay renglones repetidos contra la misma línea de la orden de compra: únalos en uno solo.");
        }

        return (orden, mapa);
    }

    /// <summary>
    /// Suma lo recibido a <c>cantidad_aplicada</c> y recalcula el estado de la orden. Como la
    /// orden admite varias facturas, esto es acumulativo: Recibida parcial mientras quede algo
    /// pendiente, Cerrada cuando todos los renglones quedan cubiertos.
    /// </summary>
    private static void AplicarDescargaOrden(
        RecepcionCompraDto dto, alm_orden_compra? orden, Dictionary<int, alm_orden_compra_detalle> pendientes)
    {
        if (orden is null || pendientes.Count == 0) return;

        foreach (var d in dto.Detalles)
        {
            var renglon = pendientes[d.OrdenDetalleId!.Value];
            renglon.cantidad_aplicada += d.Cantidad;
        }

        var todoCubierto = pendientes.Values.All(r => r.cantidad_aplicada >= r.cantidad_pedida);
        orden.estado = todoCubierto ? EstadoOrdenCompra.Cerrada : EstadoOrdenCompra.RecibidaParcial;
        orden.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
    }

    /// <summary>
    /// Arma las líneas de <c>alm_compra</c> y los totales de la cabecera. Devuelve cada línea
    /// junto al costo con el que debe entrar al kardex (con ISV capitalizado o sin él).
    /// </summary>
    private static List<(alm_compra Linea, decimal CostoPosteo)> ArmarLineasYTotales(
        alm_compra_hdr cabecera,
        RecepcionCompraDto dto,
        IReadOnlyDictionary<int, string> upcs,
        IReadOnlyDictionary<int, string?> codigos,
        IReadOnlyDictionary<int, TasaIsvArticulo> tasas,
        bool capitalizaIsv)
    {
        var resultado = new List<(alm_compra, decimal)>();
        decimal subTotal = 0m, impuestoTotal = 0m;

        foreach (var d in dto.Detalles)
        {
            var totalRenglon = Redondear2(d.Cantidad * d.CostoUnitario);
            var porcentaje = tasas.TryGetValue(d.ArticuloId, out var t) ? t.Porcentaje : 0m;
            var isvRenglon = Redondear2(totalRenglon * porcentaje / 100m);

            ExigirMonto(totalRenglon, "El total de un renglón");
            ExigirMonto(isvRenglon, "El impuesto de un renglón");

            subTotal += totalRenglon;
            impuestoTotal += isvRenglon;

            // Costo del asiento: el ISV entra al costo solo si la política lo capitaliza.
            // Se prorratea por unidad, que es lo que pondera el promedio móvil.
            var costoPosteo = capitalizaIsv && d.Cantidad > 0m
                ? d.CostoUnitario + (isvRenglon / d.Cantidad)
                : d.CostoUnitario;

            var linea = new alm_compra
            {
                fecha = cabecera.fecha,
                fecha_factura = cabecera.fecha_factura,
                articulo_id = d.ArticuloId,
                codigo_articulo = codigos.TryGetValue(d.ArticuloId, out var cod)
                    ? ClasificacionNormalizer.Opcional(cod, 20)
                    : null,
                cantidad = d.Cantidad,
                precio_unitario = d.CostoUnitario,
                total = totalRenglon,
                impuesto = isvRenglon,
                descuento = d.Descuento,
                proveedor = cabecera.proveedor ?? cabecera.cod_proveedor,
                plazo_dias = cabecera.plazo_dias,
                tipo_compra = cabecera.tipo_compra,
                concepto = ClasificacionNormalizer.Opcional(d.Descripcion, 254),
                bodega_id = cabecera.bodega_id,
                // La FK a la cabecera NO se asigna a mano: la cabecera todavía no tiene id
                // (se genera en el SaveChanges). Se fija la navegación y EF resuelve la FK al
                // materializar ambas en el mismo Add.
                orden_compra_detalle_id = d.OrdenDetalleId,
                // Identidad de la línea, generada al CREAR (no al postear). El asiento del
                // kardex deriva su propio uuid del id de esta línea.
                uuid = Guid.NewGuid(),
                origen = OrigenDocumento.Siad,
                posteado = false,
                cuenta_contable = null
            };
            linea.cabecera = cabecera;

            // Los snapshots salen del catálogo, no del cliente.
            if (upcs.TryGetValue(d.ArticuloId, out var upc))
            {
                d.CodigoUpc = upc;
            }
            d.CodigoArticulo = linea.codigo_articulo;
            d.Impuesto = isvRenglon;
            d.Total = totalRenglon;
            d.CostoPosteado = costoPosteo;

            resultado.Add((linea, costoPosteo));
        }

        var montoDescuento = Redondear2(subTotal * cabecera.descuento / 100m);
        cabecera.sub_total = Redondear2(subTotal);
        cabecera.impuesto = Redondear2(impuestoTotal);
        cabecera.total = Redondear2(subTotal - montoDescuento + impuestoTotal
                                    + cabecera.otros_gastos + cabecera.flete_seguro);

        ExigirMonto(cabecera.sub_total, "El subtotal de la factura");
        ExigirMonto(cabecera.impuesto, "El impuesto de la factura");
        ExigirMonto(cabecera.total, "El total de la factura");

        return resultado;
    }

    /// <summary>
    /// ¿El ISV se suma al costo? Capa 2 de la política: lo decide la empresa
    /// (<c>cfg_compra_isv</c>), y la factura puede invertirlo con "Detallar ISV".
    /// Detallar = el ISV va aparte, NO capitaliza.
    /// </summary>
    private async Task<bool> ResolverSiCapitalizaIsvAsync(bool? detallarIsv, CancellationToken ct)
    {
        if (detallarIsv.HasValue) return !detallarIsv.Value;

        var cfg = await _context.cfg_compra_isvs.AsNoTracking().FirstOrDefaultAsync(ct);
        var tratamiento = cfg?.tratamiento ?? TratamientoIsvCompra.Costo;
        return tratamiento == TratamientoIsvCompra.Costo;
    }

    /// <summary>
    /// Id de la fila (artículo, bodega) donde entra la mercadería. Si el artículo todavía no
    /// tiene ubicación en esa bodega se CREA: comprar hacia una bodega donde el artículo aún
    /// no está es un caso normal, y el motor exige que la fila exista.
    /// </summary>
    private async Task<int> ResolverParAsync(
        int articuloId, int bodegaId, string usuario, DateTime ahora, CancellationToken ct)
    {
        var fila = await _context.alm_articulo_bodegas
            .FirstOrDefaultAsync(u => u.articulo_id == articuloId && u.bodega_id == bodegaId, ct);

        if (fila is not null)
        {
            if (!fila.activo)
            {
                throw new InvalidOperationException(
                    "La ubicación del artículo en esa bodega está deshabilitada: reactívela antes de recibir.");
            }
            return fila.id;
        }

        // Principal solo si el artículo no tiene ya otra ubicación activa (hay una sola
        // principal por artículo).
        var tieneOtra = await _context.alm_articulo_bodegas.AsNoTracking()
            .AnyAsync(u => u.articulo_id == articuloId && u.activo, ct);

        fila = new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = bodegaId,
            existencia = 0m,
            costo_promedio = 0m,
            activo = true,
            principal = !tieneOtra,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        _context.alm_articulo_bodegas.Add(fila);
        await _context.SaveChangesAsync(ct);
        return fila.id;
    }

    /// <summary>
    /// Siguiente número de la empresa: alta idempotente del contador y luego
    /// <c>SELECT … FOR UPDATE</c> para serializar dos altas concurrentes (mismo patrón que la
    /// O/C). El <c>company_id</c> va DENTRO del SQL crudo por el filtro de tenant de EF.
    /// </summary>
    private async Task<int> SiguienteNumeroAsync(long companyId, CancellationToken ct)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO alm_compra_correlativo (company_id, ultimo_numero)
            VALUES ({companyId}, 0)
            ON CONFLICT (company_id) DO NOTHING", ct);

        var contador = await _context.alm_compra_correlativos
            .FromSqlInterpolated($@"
                SELECT * FROM alm_compra_correlativo
                 WHERE company_id = {companyId}
                 FOR UPDATE")
            .FirstAsync(ct);

        contador.ultimo_numero += 1;
        return contador.ultimo_numero;
    }

    private static RecepcionCompraDto MapCabecera(alm_compra_hdr h) => new()
    {
        Id = h.id,
        Numero = h.numero,
        Fecha = h.fecha,
        FechaFactura = h.fecha_factura,
        FechaVencimiento = h.fecha_vencimiento,
        CodProveedor = h.cod_proveedor,
        ProveedorNombre = h.proveedor,
        NumeroFacturaSar = h.numero_factura_sar,
        Cai = h.cai,
        OrdenCompraId = h.orden_compra_id,
        OrdenCompraNumero = h.orden?.numero,
        BodegaId = h.bodega_id,
        BodegaNombre = h.bodega_ref?.nombre,
        TerminoPagoId = h.termino_pago_id,
        TerminosPago = h.terminos_pago,
        TipoCompra = h.tipo_compra,
        CondicionPago = h.condicion_pago,
        PlazoDias = h.plazo_dias,
        Moneda = h.moneda,
        TasaCambio = h.tasa_cambio,
        ConsumoInterno = h.consumo_interno,
        DetallarIsv = h.detallar_isv,
        Descuento = h.descuento,
        OtrosGastos = h.otros_gastos,
        FleteSeguro = h.flete_seguro,
        SubTotal = h.sub_total,
        Impuesto = h.impuesto,
        Total = h.total,
        Observaciones = h.observaciones,
        Estado = h.estado,
        Posteado = h.posteado,
        Uuid = h.uuid
    };

    /// <summary>Código de cada artículo tal como está en el catálogo (snapshot de la línea).</summary>
    private async Task<IReadOnlyDictionary<int, string?>> CodigosArticuloAsync(
        List<int> articuloIds, CancellationToken ct)
    {
        if (articuloIds.Count == 0) return new Dictionary<int, string?>();

        var filas = await _context.alm_articulos.AsNoTracking()
            .Where(a => articuloIds.Contains(a.id))
            .Select(a => new { a.id, a.codigo_articulo })
            .ToListAsync(ct);

        return filas.ToDictionary(a => a.id, a => a.codigo_articulo);
    }

    private async Task<string?> NombreProveedorAsync(string cod, long companyId, CancellationToken ct)
        => await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == companyId && p.cod_proveedor == cod)
            .Select(p => p.nombre)
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Aplica el término de pago del catálogo a la cabecera: valida que exista y esté activo,
    /// snapshotea su nombre y días, deriva contado/crédito y calcula el vencimiento (fecha de la
    /// factura + días) cuando el usuario no lo fijó a mano. Sin término elegido no toca nada.
    /// </summary>
    private async Task AplicarTerminoPagoAsync(
        alm_compra_hdr cabecera, RecepcionCompraDto dto, DateOnly fecha, CancellationToken ct)
    {
        if (!dto.TerminoPagoId.HasValue) return;

        // El filtro por empresa lo aplica el contexto: un término de otra empresa no aparece.
        var termino = await _context.alm_termino_pagos.AsNoTracking()
            .FirstOrDefaultAsync(t => t.id == dto.TerminoPagoId.Value, ct)
            ?? throw new InvalidOperationException("El término de pago seleccionado no existe.");
        if (!termino.activo)
        {
            throw new InvalidOperationException("El término de pago seleccionado está inactivo.");
        }

        cabecera.termino_pago_id = termino.id;
        cabecera.terminos_pago = termino.nombre;   // el snapshot del catálogo manda sobre el texto libre
        cabecera.plazo_dias = termino.dias;
        cabecera.tipo_compra = termino.dias > 0 ? (short)1 : TipoCompra.Contado;
        cabecera.condicion_pago = termino.dias > 0 ? CondicionPagoCompra.Credito : CondicionPagoCompra.Contado;

        // El vencimiento lo define el término; si el usuario lo ajustó a mano, se respeta.
        cabecera.fecha_vencimiento ??= (dto.FechaFactura ?? fecha).AddDays(termino.dias);
    }

    /// <summary>Resuelve en un solo viaje el nombre de varios proveedores por su código.</summary>
    private async Task ResolverProveedoresAsync(
        IEnumerable<string> codigos, Action<string, string?> asignar, CancellationToken ct)
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

    private async Task ValidarProveedorAsync(string cod, long companyId, CancellationToken ct)
    {
        var existe = await _context.prv_proveedores.AsNoTracking()
            .AnyAsync(p => p.company_id == companyId && p.cod_proveedor == cod && (p.status == null || p.status == true), ct);
        if (!existe)
        {
            throw new InvalidOperationException("El proveedor seleccionado no existe o está inactivo.");
        }
    }

    private async Task<alm_bodega> ValidarBodegaAsync(int bodegaId, CancellationToken ct)
    {
        if (bodegaId <= 0)
        {
            throw new InvalidOperationException("Debe indicar la bodega que recibe la mercadería.");
        }
        var bodega = await _context.alm_bodegas.AsNoTracking()
            .FirstOrDefaultAsync(b => b.id == bodegaId, ct)
            ?? throw new InvalidOperationException("La bodega indicada no existe.");
        if (!bodega.activo)
        {
            throw new InvalidOperationException("La bodega indicada está deshabilitada.");
        }
        return bodega;
    }

    private static void ValidarCabecera(RecepcionCompraDto dto)
    {
        if (dto.Descuento < 0m || dto.Descuento > 100m)
        {
            throw new InvalidOperationException("El descuento debe estar entre 0 y 100 por ciento.");
        }
        if (dto.OtrosGastos < 0m || dto.FleteSeguro < 0m)
        {
            throw new InvalidOperationException("Los otros gastos y el flete no pueden ser negativos.");
        }
        if (!MonedaCompra.EsValida(dto.Moneda))
        {
            throw new InvalidOperationException("La moneda debe ser HNL o USD.");
        }
        if (dto.TasaCambio <= 0m)
        {
            throw new InvalidOperationException("La tasa de cambio debe ser mayor que cero.");
        }
        if (dto.PlazoDias is < 0)
        {
            throw new InvalidOperationException("El plazo en días no puede ser negativo.");
        }
        ExigirMonto(dto.OtrosGastos, "Los otros gastos");
        ExigirMonto(dto.FleteSeguro, "El flete y seguro");
    }

    /// <summary>
    /// Regla D-B: solo se compra un artículo a un proveedor si existe en almacén CON su
    /// código de proveedor (relación activa y <c>codigo_upc</c> con valor). Devuelve el mapa
    /// artículo → código, que se usa como snapshot.
    /// </summary>
    private async Task<IReadOnlyDictionary<int, string>> ValidarRenglonesAsync(
        IReadOnlyCollection<RecepcionCompraDetalleDto> detalles, string cod, CancellationToken ct)
    {
        if (detalles is null || detalles.Count == 0)
        {
            throw new InvalidOperationException("La factura debe tener al menos un renglón.");
        }
        foreach (var d in detalles)
        {
            if (d.ArticuloId <= 0)
            {
                throw new InvalidOperationException("Hay un renglón sin artículo.");
            }
            if (d.Cantidad <= 0m)
            {
                throw new InvalidOperationException("La cantidad recibida debe ser mayor que cero.");
            }
            // Regla D-E: el motor rechaza costo 0 porque corrompería el promedio ponderado,
            // y el kardex es inmutable. Se valida aquí para fallar antes de escribir nada.
            if (d.CostoUnitario <= 0m)
            {
                throw new InvalidOperationException("El costo unitario debe ser mayor que cero.");
            }
            if (d.Descuento < 0m)
            {
                throw new InvalidOperationException("El descuento de un renglón no puede ser negativo.");
            }
            ExigirCantidad(d.Cantidad, "La cantidad recibida");
            ExigirCantidad(d.CostoUnitario, "El costo unitario");
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

    /// <summary>Tope de <c>NUMERIC(14,2)</c> (12 dígitos enteros), exclusivo.</summary>
    private const decimal MaxMonto = 1_000_000_000_000m;

    /// <summary>Tope de <c>NUMERIC(11,4)</c> de <c>alm_compra.precio_unitario</c>, exclusivo.</summary>
    private const decimal MaxCantidad = 10_000_000m;

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

    private static decimal Redondear2(decimal v) => Math.Round(v, 2, MidpointRounding.AwayFromZero);
}
