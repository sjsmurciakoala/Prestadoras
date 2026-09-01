using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Core.Utilities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Motor de posteo del inventario. Ver <see cref="IInventarioPostingService"/>.
/// <para>
/// Secuencia invariable de <see cref="PostearAsync"/>: derivar uuid → si ya existe, salir →
/// bloquear la fila con <c>FOR UPDATE</c> → validar → calcular → aplicar sobre
/// <c>alm_articulo_bodega</c> → insertar el asiento → rollup de cabecera → un solo
/// <c>SaveChanges</c>.
/// </para>
/// </summary>
public sealed class InventarioPostingService : IInventarioPostingService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IArticuloRollupService _rollup;

    public InventarioPostingService(SiadDbContext context, ICurrentCompanyService company, IArticuloRollupService rollup)
    {
        _context = context;
        _company = company;
        _rollup = rollup;
    }

    public async Task<PosteoResultDto> PostearAsync(MovimientoInventarioDto movimiento, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(movimiento);

        if (movimiento.Tipo is not (TipoMovimientoInventario.CargaInicialNueva
            or TipoMovimientoInventario.CargaInicialReconciliacion
            or TipoMovimientoInventario.AjustePositivo
            or TipoMovimientoInventario.AjusteNegativo
            or TipoMovimientoInventario.AjusteValor
            or TipoMovimientoInventario.Reversa
            or TipoMovimientoInventario.Compra
            or TipoMovimientoInventario.SalidaDescargo
            or TipoMovimientoInventario.TrasladoSalida
            or TipoMovimientoInventario.TrasladoEntrada))
        {
            throw new NotSupportedException(
                $"El motor todavía no postea movimientos de tipo {movimiento.Tipo}. En esta entrega hay carga inicial, ajustes, reversa, compra, salida por descargo y traslado.");
        }

        var companyId = _company.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo resolver la empresa actual.");
        }

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // ── 1. uuid determinista + corte por idempotencia ────────────────────
        var uuid = DerivarUuid(movimiento, companyId);

        var existente = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.uuid == uuid)
            .Select(k => new { k.id, k.existencia_resultante, k.costo_promedio_resultante })
            .FirstOrDefaultAsync(ct);

        if (existente is not null)
        {
            // Reintento del mismo posteo: no se escribe nada, se devuelve lo ya asentado.
            await TransaccionAmbiente.ConfirmarAsync(tx, ct);
            return new PosteoResultDto
            {
                KardexId = existente.id,
                Uuid = uuid,
                YaExistia = true,
                ExistenciaResultante = existente.existencia_resultante ?? 0m,
                CostoPromedioResultante = existente.costo_promedio_resultante ?? 0m
            };
        }

        // ── 2. Bloquear la fila del par ──────────────────────────────────────
        var fila = await BloquearArticuloBodegaAsync(movimiento.ArticuloBodegaId, companyId, ct);

        // ── 3. Validar ───────────────────────────────────────────────────────
        // Devuelve el asiento original SOLO en la reversa: el cálculo lo necesita para saber
        // en qué dirección revertir y con qué cantidad/costo, que salen de lo posteado y no
        // de lo que mande el llamador.
        var original = await ValidarAsync(movimiento, fila, ct);

        // ── 4. Calcular ──────────────────────────────────────────────────────
        var (existenciaResultante, costoPromedioResultante, ingresos, salidas, costoAsiento) =
            Calcular(movimiento, fila, original);

        // Cruce a alerta: estado del par ANTES (existencia previa, aún en fila) vs DESPUÉS. Se marca
        // cuando pasa de "en orden" a alerta (anti-spam: no en cada salida estando ya bajo), MÁS una
        // excepción (F3, 2026-08-15): cruzar a NEGATIVA siempre avisa aunque ya estuviera en alerta
        // (bajo mínimo o sin stock → negativa), porque el negativo es la anomalía a reconciliar. Una
        // caída DENTRO de negativo (-2 → -5) no re-avisa (severidadAntes ya es Negativa).
        var severidadAntes = StockSeveridad.Clasificar(fila.existencia, fila.existencia_minima);
        var severidadDespues = StockSeveridad.Clasificar(existenciaResultante, fila.existencia_minima);
        var cruzoAlerta = (severidadAntes is null && severidadDespues is not null)
            || (severidadDespues == StockSeveridad.Negativa && severidadAntes != StockSeveridad.Negativa);

        // ── 5. Aplicar sobre la fila (ASIGNACIÓN, nunca +=) ──────────────────
        fila.existencia = existenciaResultante;
        fila.costo_promedio = costoPromedioResultante;
        if (ingresos > 0)
        {
            fila.ultimo_costo = costoAsiento;
        }
        fila.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        fila.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        // ── 6. Insertar el asiento (INSERT y nada más: el kardex es inmutable) ─
        // La reversa asienta LO QUE SE POSTEÓ, no lo que dice el DTO: si el llamador mandara
        // otra cifra, des-postearía algo distinto de lo posteado y nada lo detectaría (el uuid
        // REVERSA|company|kardexId congela el primer intento que llegue).
        var cantidadAsiento = movimiento.Tipo == TipoMovimientoInventario.Reversa && original is not null
            ? original.cantidad
            : movimiento.Cantidad;

        var articulo = await _context.alm_articulos.AsNoTracking()
            .Where(a => a.id == fila.articulo_id)
            .Select(a => new { a.codigo_articulo, a.cuenta_contable })
            .FirstAsync(ct);

        var asiento = new alm_kardex
        {
            articulo_id = fila.articulo_id,
            bodega_id = fila.bodega_id,
            codigo_articulo = articulo.codigo_articulo,
            fecha = movimiento.Fecha,
            tipo_transaccion = TipoTransaccionDe(movimiento.Tipo, original),
            // La reversa se identifica SIEMPRE con su propio vocabulario y apunta al asiento
            // que anula: así la cubre ix_alm_kardex_reversa y la guarda de apertura vigente
            // puede cruzarlas. No se acepta lo que mande el llamador. La compra hace lo mismo
            // por la razón inversa: su trazabilidad es lo único que la distingue de un ajuste
            // positivo, así que el tipo de documento no puede quedar a criterio del llamador.
            documento_tipo = movimiento.Tipo switch
            {
                TipoMovimientoInventario.Reversa => TipoDocumentoInventario.Reversa,
                TipoMovimientoInventario.Compra => TipoDocumentoInventario.Compra,
                TipoMovimientoInventario.SalidaDescargo => TipoDocumentoInventario.Descargo,
                // Los dos lados del traslado son TRASLADO en el libro (fijo, como Compra/Descargo):
                // su trazabilidad es lo único que los distingue de un ajuste, y no puede quedar a
                // criterio del llamador.
                TipoMovimientoInventario.TrasladoSalida => TipoDocumentoInventario.Traslado,
                TipoMovimientoInventario.TrasladoEntrada => TipoDocumentoInventario.Traslado,
                _ => movimiento.DocumentoTipo
            },
            documento_id = movimiento.Tipo == TipoMovimientoInventario.Reversa
                ? movimiento.KardexIdRevertido!.Value
                : movimiento.DocumentoId,
            // Solo informativa: la bodega afectada por este asiento es bodega_id (la del par).
            bodega_destino_id = movimiento.BodegaDestinoId,
            uuid = uuid,
            cantidad = cantidadAsiento,
            ingresos = ingresos,
            salidas = salidas,
            valor_unitario = costoAsiento,
            total = cantidadAsiento * costoAsiento,
            debe = ingresos > 0 ? cantidadAsiento * costoAsiento : 0m,
            haber = salidas > 0 ? cantidadAsiento * costoAsiento : 0m,
            existencia_resultante = existenciaResultante,
            costo_promedio_resultante = costoPromedioResultante,
            cuenta_contable = articulo.cuenta_contable,
            es_ajuste = EsAjuste(movimiento.Tipo),
            descripcion = DescripcionDe(movimiento.Tipo),
            observacion = movimiento.Observacion,
            // EF no aplica el DEFAULT de la BD en estas dos: se estampan a mano.
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.alm_kardexs.Add(asiento);

        // ── 7 y 8. Un solo SaveChanges, luego el rollup (statement aparte) ───
        await _context.SaveChangesAsync(ct);
        // El traslado difiere el rollup: lo hace el servicio una sola vez por artículo, al final y
        // en orden ascendente de articulo_id, para no abrir un deadlock ABBA sobre alm_articulo
        // entre traslados multi-bodega que tocan los mismos artículos en distinto orden.
        if (!movimiento.DeferirRollup)
        {
            await _rollup.RecomputeAsync(fila.articulo_id, ct);
        }

        await TransaccionAmbiente.ConfirmarAsync(tx, ct);

        return new PosteoResultDto
        {
            KardexId = asiento.id,
            Uuid = uuid,
            YaExistia = false,
            ExistenciaResultante = existenciaResultante,
            CostoPromedioResultante = costoPromedioResultante,
            CruzoAlerta = cruzoAlerta,
            SeveridadAlerta = cruzoAlerta ? severidadDespues : null
        };
    }

    public async Task<bool> TieneAperturaVigenteAsync(int articuloId, int bodegaId, CancellationToken ct = default)
    {
        // Apertura vigente = un CARGA_INICIAL del par SIN una REVERSA que lo apunte.
        // Se cruzan por id: la reversa lleva documento_tipo = 'REVERSA' y documento_id = el
        // id del asiento que anula (cubierto por ix_alm_kardex_reversa).
        var aperturas = await AperturasDelParAsync(articuloId, bodegaId, ct);
        if (aperturas.Count == 0)
        {
            return false;
        }

        var revertidas = await IdsRevertidosAsync(aperturas, ct);
        return aperturas.Exists(id => !revertidas.Contains(id));
    }

    /// <summary>
    /// Cuántas aperturas del par ya fueron revertidas. Es el discriminador de intento del
    /// uuid: permite reabrir un par sin chocar con el índice único.
    /// </summary>
    public async Task<int> ContarAperturasRevertidasAsync(int articuloId, int bodegaId, CancellationToken ct = default)
    {
        var aperturas = await AperturasDelParAsync(articuloId, bodegaId, ct);
        if (aperturas.Count == 0)
        {
            return 0;
        }

        return (await IdsRevertidosAsync(aperturas, ct)).Count;
    }

    /// <summary>Ids de los asientos CARGA_INICIAL del par (entradas, no reversas).</summary>
    private async Task<List<int>> AperturasDelParAsync(int articuloId, int bodegaId, CancellationToken ct)
        => await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId
                     && k.bodega_id == bodegaId
                     && k.documento_tipo == TipoDocumentoInventario.CargaInicial)
            .Select(k => k.id)
            .ToListAsync(ct);

    /// <summary>De esos ids, cuáles tienen una REVERSA apuntándolos.</summary>
    private async Task<HashSet<int>> IdsRevertidosAsync(List<int> aperturaIds, CancellationToken ct)
        => (await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.documento_tipo == TipoDocumentoInventario.Reversa
                     && k.documento_id != null
                     && aperturaIds.Contains(k.documento_id.Value))
            .Select(k => k.documento_id!.Value)
            .ToListAsync(ct)).ToHashSet();

    // ── Piezas internas ──────────────────────────────────────────────────────

    /// <summary>
    /// Bloquea la fila con <c>FOR UPDATE</c>. El <c>company_id</c> va DENTRO del SQL crudo:
    /// EF compone su filtro de tenant por encima de la consulta, así que sin esto el candado
    /// se tomaría antes de filtrar por empresa. El bloqueo no es opcional: sin él, dos
    /// posteos concurrentes calculan el mismo resultado y el índice único no los detiene
    /// (sus uuid difieren).
    /// </summary>
    private async Task<alm_articulo_bodega> BloquearArticuloBodegaAsync(int articuloBodegaId, long companyId, CancellationToken ct)
    {
        var fila = await _context.alm_articulo_bodegas
            .FromSqlInterpolated($@"
                SELECT * FROM alm_articulo_bodega
                 WHERE company_id = {companyId} AND id = {articuloBodegaId}
                 FOR UPDATE")
            .FirstOrDefaultAsync(ct);

        return fila ?? throw new InvalidOperationException(
            "La ubicación (artículo/bodega) no existe en la empresa actual.");
    }

    /// <summary>
    /// Valida el movimiento y, en la reversa, DEVUELVE el asiento original: el cálculo decide
    /// con él la dirección (revertir una salida entra, revertir una entrada sale) y toma de ahí
    /// la cantidad y el costo. En el resto de los tipos devuelve null.
    /// </summary>
    private async Task<alm_kardex?> ValidarAsync(MovimientoInventarioDto m, alm_articulo_bodega fila, CancellationToken ct)
    {
        if (m.Cantidad < 0)
        {
            throw new InvalidOperationException("La cantidad del movimiento no puede ser negativa.");
        }

        switch (m.Tipo)
        {
            case TipoMovimientoInventario.CargaInicialNueva:
                // La apertura vigente se comprueba PRIMERO: es la condición más específica y
                // su mensaje es el accionable ("revierta y vuelva a abrir"). Si se validara
                // después, un segundo intento sobre un par ya abierto saltaría con el error
                // genérico de "existencia previa 0", que manda al usuario por el camino
                // equivocado (a reconciliar, cuando lo que necesita es revertir).
                await ExigirSinAperturaVigenteAsync(fila, ct);
                if (fila.existencia != 0m)
                {
                    throw new InvalidOperationException(
                        $"La apertura de una ubicación nueva exige existencia previa 0, y tiene {fila.existencia:0.####}. Use la apertura por reconciliación.");
                }
                ExigirCantidadPositiva(m);
                ExigirCostoPositivo(m);
                break;

            case TipoMovimientoInventario.CargaInicialReconciliacion:
                await ExigirSinAperturaVigenteAsync(fila, ct);
                if (fila.existencia <= 0m)
                {
                    throw new InvalidOperationException(
                        "La apertura por reconciliación exige que la ubicación ya tenga existencia positiva.");
                }
                if (m.Cantidad != fila.existencia)
                {
                    // La reconciliación DESCRIBE lo que ya hay: no acepta que le dicten la cifra.
                    throw new InvalidOperationException(
                        $"La reconciliación debe postear exactamente la existencia registrada ({fila.existencia:0.####}), no {m.Cantidad:0.####}.");
                }
                ExigirCostoPositivo(m);
                break;

            case TipoMovimientoInventario.AjustePositivo:
                ExigirCantidadPositiva(m);
                ExigirCostoPositivo(m);
                break;

            case TipoMovimientoInventario.Compra:
                ExigirCantidadPositiva(m);
                // Regla D-E del diseño de recepción: una compra a costo 0 corrompería el
                // promedio ponderado del par, y el kardex es inmutable (no hay UPDATE que lo
                // arregle después).
                ExigirCostoPositivo(m);
                if (m.DocumentoId <= 0)
                {
                    // Sin documento no hay uuid estable: dos reintentos de la misma recepción
                    // entrarían dos veces al inventario.
                    throw new InvalidOperationException(
                        "La compra debe indicar la línea de recepción (documento) que la origina.");
                }
                break;

            case TipoMovimientoInventario.AjusteNegativo:
                ExigirCantidadPositiva(m);
                // Regla firme de inventario: una salida NUNCA puede dejar la existencia en negativo.
                if (fila.existencia - m.Cantidad < 0m)
                {
                    throw new InvalidOperationException(
                        $"El ajuste dejaría la existencia en negativo ({fila.existencia - m.Cantidad:0.####}).");
                }
                break;

            case TipoMovimientoInventario.AjusteValor:
                if (m.Cantidad != 0m)
                {
                    throw new InvalidOperationException("El ajuste de valor no mueve existencia: la cantidad debe ser 0.");
                }
                ExigirCostoPositivo(m);
                break;

            case TipoMovimientoInventario.SalidaDescargo:
                ExigirCantidadPositiva(m);
                if (m.DocumentoId <= 0)
                {
                    // Sin documento no hay uuid estable: dos reintentos del mismo despacho
                    // sacarían la mercadería dos veces.
                    throw new InvalidOperationException(
                        "La salida debe indicar la línea de descargo (documento) que la origina.");
                }
                if (fila.costo_promedio <= 0m)
                {
                    // Salir a costo 0 graba un asiento sin valor en un libro inmutable y
                    // descuadra la valorización para siempre. En el mirror, 241 de los 245
                    // pares con existencia están así hasta que corra el corte de inventario.
                    throw new InvalidOperationException(
                        "La ubicación no tiene costo promedio: no se puede despachar hasta que se le asigne uno (carga inicial o ajuste de valor).");
                }
                // Regla firme de inventario: una salida por descargo NUNCA puede dejar la existencia
                // en negativo. No hay confirmación en pantalla ni interruptor que lo habilite.
                if (fila.existencia - m.Cantidad < 0m)
                {
                    throw new InvalidOperationException(
                        $"La salida dejaría la existencia en negativo ({fila.existencia - m.Cantidad:0.####}). Disponible: {fila.existencia:0.####}.");
                }
                break;

            case TipoMovimientoInventario.TrasladoSalida:
                // Lado ENVÍO: misma guarda que una salida por descargo. Sale al promedio vigente de
                // origen, así que si el par no tiene costo, trasladarlo corrompería el promedio del
                // destino (y el kardex es inmutable).
                ExigirCantidadPositiva(m);
                if (m.DocumentoId <= 0)
                {
                    throw new InvalidOperationException(
                        "El traslado (salida) debe indicar la línea de traslado (documento) que lo origina.");
                }
                if (fila.costo_promedio <= 0m)
                {
                    throw new InvalidOperationException(
                        "La ubicación de origen no tiene costo promedio: no se puede trasladar hasta que se le asigne uno (carga inicial o ajuste de valor).");
                }
                if (fila.existencia - m.Cantidad < 0m)
                {
                    throw new InvalidOperationException(
                        $"El traslado dejaría la existencia de origen en negativo ({fila.existencia - m.Cantidad:0.####}). Disponible: {fila.existencia:0.####}.");
                }
                break;

            case TipoMovimientoInventario.TrasladoEntrada:
                // Lado RECEPCIÓN: entra a destino al costo con que salió de origen (viaja con la
                // mercadería). Misma exigencia que una compra: cantidad y costo positivos y un
                // documento (la línea de recepción) que ancle la idempotencia.
                ExigirCantidadPositiva(m);
                ExigirCostoPositivo(m);
                if (m.DocumentoId <= 0)
                {
                    throw new InvalidOperationException(
                        "El traslado (entrada) debe indicar la línea de recepción (documento) que lo origina.");
                }
                break;

            case TipoMovimientoInventario.Reversa:
                if (!m.KardexIdRevertido.HasValue)
                {
                    throw new InvalidOperationException("La reversa debe indicar qué asiento revierte.");
                }
                var original = await _context.alm_kardexs.AsNoTracking()
                    .FirstOrDefaultAsync(k => k.id == m.KardexIdRevertido.Value, ct)
                    ?? throw new InvalidOperationException("El asiento a revertir no existe en la empresa actual.");

                if (original.articulo_id != fila.articulo_id || original.bodega_id != fila.bodega_id)
                {
                    throw new InvalidOperationException("El asiento a revertir pertenece a otro artículo o bodega.");
                }
                if (original.documento_tipo == TipoDocumentoInventario.Reversa)
                {
                    // Revertir una reversa re-aplicaría el movimiento anulado por una vía que
                    // nadie audita. Si hay que rehacerlo, se postea el documento de nuevo.
                    throw new InvalidOperationException("No se puede revertir una reversa.");
                }
                // Revertir una ENTRADA saca de la bodega: si ya no está, no hay nada que devolver.
                // Revertir una SALIDA (o el lado de ENVÍO de un traslado, salidas>0) SUMA, así que
                // nunca deja negativo y NO debe pasar por esta guarda. Sin este tercer sitio, anular
                // un traslado que despachó el grueso del stock de origen fallaría siempre («la
                // mercadería ya salió»), porque el origen ya está en 0. Ver diseño §3, sitio 3.
                if (!EsReversaDeDevolucion(original) && fila.existencia - original.cantidad < 0m)
                {
                    throw new InvalidOperationException(
                        $"La reversa dejaría la existencia en negativo ({fila.existencia - original.cantidad:0.####}): la mercadería ya salió. Corrija con un ajuste.");
                }
                return original;
        }

        return null;
    }

    private static void ExigirCantidadPositiva(MovimientoInventarioDto m)
    {
        if (m.Cantidad <= 0m)
        {
            throw new InvalidOperationException("La cantidad del movimiento debe ser mayor que cero.");
        }
    }

    private static void ExigirCostoPositivo(MovimientoInventarioDto m)
    {
        // Regla del plan: una apertura con costo 0 NO se postea. Sembrar el inventario a
        // costo cero corrompe el promedio ponderado de la primera compra que entre después,
        // y el kardex es inmutable: no hay marcha atrás con un UPDATE.
        if (m.CostoUnitario <= 0m)
        {
            throw new InvalidOperationException(
                "El costo unitario debe ser mayor que cero: un movimiento a costo 0 corrompería el costo promedio.");
        }
    }

    private async Task ExigirSinAperturaVigenteAsync(alm_articulo_bodega fila, CancellationToken ct)
    {
        if (await TieneAperturaVigenteAsync(fila.articulo_id, fila.bodega_id, ct))
        {
            throw new InvalidOperationException(
                "La ubicación ya tiene una carga inicial vigente. Para corregirla, revierta la apertura y vuelva a abrirla.");
        }
    }

    /// <summary>
    /// Devuelve (existencia resultante, costo promedio resultante, ingresos, salidas, costo del asiento).
    /// </summary>
    private static (decimal Existencia, decimal CostoPromedio, decimal Ingresos, decimal Salidas, decimal Costo)
        Calcular(MovimientoInventarioDto m, alm_articulo_bodega fila, alm_kardex? original)
    {
        switch (m.Tipo)
        {
            case TipoMovimientoInventario.CargaInicialNueva:
                // Entrada sobre existencia 0: el borde documentado del promedio ponderado.
                // El costo promedio ES el costo de entrada (nunca se divide por cero).
                return (m.Cantidad, m.CostoUnitario, m.Cantidad, 0m, m.CostoUnitario);

            case TipoMovimientoInventario.CargaInicialReconciliacion:
                // El asiento describe lo que ya hay: la existencia NO cambia, solo se siembra
                // el costo.
                return (fila.existencia, m.CostoUnitario, m.Cantidad, 0m, m.CostoUnitario);

            case TipoMovimientoInventario.AjustePositivo:
            case TipoMovimientoInventario.Compra:
            case TipoMovimientoInventario.TrasladoEntrada:
            {
                // Promedio ponderado móvil. La compra y el lado de RECEPCIÓN de un traslado usan
                // exactamente la misma fórmula que el ajuste positivo: todas son entradas con costo
                // propio. En el traslado el costo que llega es el que se congeló al salir de origen
                // (viaja con la mercadería). El costo que llega ya trae el ISV capitalizado o no,
                // según la política del tipo de artículo y la empresa — el motor no decide eso, solo
                // pondera lo que recibe.
                var existencia = fila.existencia + m.Cantidad;
                // Borde de existencia NEGATIVA (F1, 2026-08-15): si la base es negativa, el promedio
                // ponderado clásico fabrica costos inventados o hasta negativos —p. ej. base -3 @ 10 con
                // 10 @ 20 daría ((-3*10)+(10*20))/7 = 24.28…—. Con base negativa el lote que entra
                // RE-ESTABLECE el costo (promedio = costo del lote), acotado a un costo real y nunca ≤ 0.
                // El borde de existencia resultante 0 mantiene su regla (nunca dividir por cero).
                var promedio = fila.existencia < 0m || existencia == 0m
                    ? m.CostoUnitario
                    : ((fila.existencia * fila.costo_promedio) + (m.Cantidad * m.CostoUnitario)) / existencia;
                return (existencia, promedio, m.Cantidad, 0m, m.CostoUnitario);
            }

            case TipoMovimientoInventario.AjusteNegativo:
            case TipoMovimientoInventario.SalidaDescargo:
            case TipoMovimientoInventario.TrasladoSalida:
            {
                // Una salida NO cambia el costo promedio: sale al promedio vigente. La entrega por
                // descargo y el lado de ENVÍO de un traslado usan exactamente esta fórmula — lo que
                // los distingue del ajuste es su trazabilidad (documento_tipo DESCARGO/TRASLADO,
                // es_ajuste = false), no el cálculo.
                var existencia = fila.existencia - m.Cantidad;
                return (existencia, fila.costo_promedio, 0m, m.Cantidad, fila.costo_promedio);
            }

            case TipoMovimientoInventario.AjusteValor:
                // Solo corrige el costo; la existencia queda igual.
                return (fila.existencia, m.CostoUnitario, 0m, 0m, m.CostoUnitario);

            case TipoMovimientoInventario.Reversa:
            {
                // Contra-asiento: saca la cantidad al costo con el que entró y DES-PONDERA el
                // promedio, es decir, le quita al inventario exactamente el valor que ese
                // documento le había sumado.
                //
                // Sin esto, revertir una entrada devolvía la existencia pero dejaba el
                // promedio movido: 18 u. a 56.3889 + 6 u. a 58.00 daba 24 a 56.7917, y al
                // revertir quedaban 18 a 56.7917 — L.7.25 de valor inventados por un documento
                // que ya no existe. Medido en el mirror el 2026-07-31.
                //
                // La cantidad y el costo salen del asiento ORIGINAL, no del DTO.
                var cantidad = original?.cantidad ?? m.Cantidad;
                var costo = original?.valor_unitario ?? m.CostoUnitario;

                // ESPEJO: revertir una SALIDA devuelve la mercadería a la bodega. Se discrimina por
                // documento_tipo (y, en el traslado, por salidas>0), NO por ingresos/salidas a secas,
                // porque el asiento no basta: CargaInicialReconciliacion escribe ingresos > 0 y no
                // mueve la existencia, así que mirar los ingresos vaciaría la bodega al revertir una
                // apertura por reconciliación. En el traslado sí es seguro mirar salidas: ambos lados
                // mueven existencia real.
                if (EsReversaDeDevolucion(original))
                {
                    var entra = fila.existencia + cantidad;
                    // Mismo borde de base negativa que la entrada normal: si el par ya estaba en
                    // negativo, la devolución no pondera contra esa base (promedio = costo devuelto).
                    var promedioEntrada = fila.existencia < 0m || entra == 0m
                        ? costo
                        : ((fila.existencia * fila.costo_promedio) + (cantidad * costo)) / entra;
                    return (entra, promedioEntrada, cantidad, 0m, costo);
                }

                var existencia = fila.existencia - cantidad;
                var valorRestante = (fila.existencia * fila.costo_promedio) - (cantidad * costo);

                // Con existencia 0 (revertir una apertura) no hay unidades que valorizar, y un
                // valor restante negativo sería un costo inventado: en ambos casos se conserva
                // el promedio vigente en vez de fabricar uno.
                var promedio = existencia > 0m && valorRestante > 0m
                    ? valorRestante / existencia
                    : fila.costo_promedio;

                return (existencia, promedio, 0m, cantidad, costo);
            }

            default:
                throw new NotSupportedException($"Cálculo no implementado para {m.Tipo}.");
        }
    }

    private static string TipoTransaccionDe(TipoMovimientoInventario tipo, alm_kardex? original) => tipo switch
    {
        TipoMovimientoInventario.CargaInicialNueva => TipoTransaccionKardex.EntradaInventarioInicial,
        TipoMovimientoInventario.CargaInicialReconciliacion => TipoTransaccionKardex.EntradaInventarioInicial,
        TipoMovimientoInventario.AjustePositivo => TipoTransaccionKardex.Ajuste,
        TipoMovimientoInventario.AjusteNegativo => TipoTransaccionKardex.Ajuste,
        TipoMovimientoInventario.AjusteValor => TipoTransaccionKardex.Ajuste,
        // La reversa lleva el código CONTRARIO al del asiento que anula: revertir una salida
        // (o el envío de un traslado) es una entrada (102) y revertir una entrada es una salida (202).
        TipoMovimientoInventario.Reversa =>
            EsReversaDeDevolucion(original)
                ? TipoTransaccionKardex.EntradaInventarioInicial
                : TipoTransaccionKardex.Salida,
        // 102 es el código de ENTRADA del legacy (también lo usa el inventario inicial).
        TipoMovimientoInventario.Compra => TipoTransaccionKardex.EntradaInventarioInicial,
        TipoMovimientoInventario.SalidaDescargo => TipoTransaccionKardex.Salida,
        // El traslado: envío es salida (202), recepción es entrada (102).
        TipoMovimientoInventario.TrasladoSalida => TipoTransaccionKardex.Salida,
        TipoMovimientoInventario.TrasladoEntrada => TipoTransaccionKardex.EntradaInventarioInicial,
        _ => TipoTransaccionKardex.Ajuste
    };

    /// <summary>
    /// ¿La reversa devuelve mercadería a la bodega (suma), en vez de sacarla (resta)? Es el ESPEJO:
    /// revertir CUALQUIER salida real —descargo, envío de un traslado o ajuste negativo genérico—
    /// devuelve. El discriminador es <c>salidas &gt; 0</c>: es seguro mirar las salidas —a diferencia
    /// de los ingresos, porque la apertura por reconciliación escribe <c>ingresos &gt; 0</c> sin mover
    /// existencia— ya que todo asiento con <c>salidas &gt; 0</c> sacó existencia real (y no se puede
    /// revertir una reversa, así que aquí <c>original</c> nunca es una reversa).
    /// <para>
    /// Ampliado el 2026-08-15 (F1): antes solo reconocía <c>Descargo</c>/<c>Traslado</c>, así que
    /// revertir un <c>AjusteNegativo</c> (documento_tipo AJUSTE, p. ej. la anulación de un movimiento
    /// genérico de salida) caía en la rama de RESTA y volvía a bajar la existencia. Debe usarse en los
    /// TRES sitios que discriminan la reversa: el cálculo, el código de transacción y la guarda de
    /// existencia negativa (diseño §3).
    /// </para>
    /// </summary>
    private static bool EsReversaDeDevolucion(alm_kardex? original) =>
        original?.salidas > 0m;

    private static bool EsAjuste(TipoMovimientoInventario tipo) => tipo
        is TipoMovimientoInventario.AjustePositivo
        or TipoMovimientoInventario.AjusteNegativo
        or TipoMovimientoInventario.AjusteValor;

    private static string DescripcionDe(TipoMovimientoInventario tipo) => tipo switch
    {
        TipoMovimientoInventario.CargaInicialNueva => "Carga inicial de existencias",
        TipoMovimientoInventario.CargaInicialReconciliacion => "Carga inicial de existencias (reconciliación)",
        TipoMovimientoInventario.AjustePositivo => "Ajuste de inventario (entrada)",
        TipoMovimientoInventario.AjusteNegativo => "Ajuste de inventario (salida)",
        TipoMovimientoInventario.AjusteValor => "Ajuste de costo",
        TipoMovimientoInventario.Reversa => "Reversa de asiento",
        TipoMovimientoInventario.Compra => "Compra a proveedor",
        TipoMovimientoInventario.SalidaDescargo => "Salida por requisición",
        TipoMovimientoInventario.TrasladoSalida => "Traslado entre bodegas (salida)",
        TipoMovimientoInventario.TrasladoEntrada => "Traslado entre bodegas (entrada)",
        _ => "Movimiento de inventario"
    };

    /// <summary>
    /// uuid determinista. La apertura se identifica por el PAR (no por la fila), más el
    /// discriminador de intento, para que reabrir tras una reversa no choque con el índice
    /// único. El resto se identifica por su documento.
    /// </summary>
    private static Guid DerivarUuid(MovimientoInventarioDto m, long companyId) => m.Tipo switch
    {
        TipoMovimientoInventario.CargaInicialNueva or TipoMovimientoInventario.CargaInicialReconciliacion =>
            UuidV5.CreateInventario(
                $"{TipoDocumentoInventario.CargaInicial}|{companyId}|{m.ArticuloBodegaId}|{m.Intento}"),

        TipoMovimientoInventario.Reversa =>
            UuidV5.CreateInventario($"REVERSA|{companyId}|{m.KardexIdRevertido}"),

        // La compra se identifica por su línea de recepción (alm_compra.id), con el tipo de
        // documento FIJO — el mismo que se estampa en el asiento. Si se tomara
        // m.DocumentoTipo, un llamador que mandara otra cadena generaría un uuid distinto
        // para la misma línea y la volvería a postear.
        TipoMovimientoInventario.Compra =>
            UuidV5.CreateInventario(
                $"{TipoDocumentoInventario.Compra}|{companyId}|{m.DocumentoId}|{m.ArticuloBodegaId}"),

        // La salida se identifica por su LÍNEA DE DESCARGO, nunca por la de requisición: una
        // misma línea de requisición se entrega en varios descargos (28 casos en el histórico),
        // y anclarla a la requisición haría que el segundo despacho se tomara por un reintento
        // del primero — mercadería fuera de la bodega sin asiento que la respalde.
        TipoMovimientoInventario.SalidaDescargo =>
            UuidV5.CreateInventario(
                $"{TipoDocumentoInventario.Descargo}|{companyId}|{m.DocumentoId}|{m.ArticuloBodegaId}"),

        // Los dos lados del traslado llevan documento_tipo TRASLADO en el asiento, pero prefijos
        // de uuid DISTINTOS y anclados a documentos distintos: la salida a la LÍNEA DEL TRASLADO
        // (alm_movimiento_dtl.id) y la entrada a la LÍNEA DE RECEPCIÓN (alm_traslado_recepcion_dtl.id).
        // Prefijos distintos + par distinto (origen vs destino) evitan cualquier colisión entre los
        // dos lados y entre los dos modos (directo vs con recepción).
        TipoMovimientoInventario.TrasladoSalida =>
            UuidV5.CreateInventario(
                $"TRASLADO_SALIDA|{companyId}|{m.DocumentoId}|{m.ArticuloBodegaId}"),

        TipoMovimientoInventario.TrasladoEntrada =>
            UuidV5.CreateInventario(
                $"TRASLADO_ENTRADA|{companyId}|{m.DocumentoId}|{m.ArticuloBodegaId}"),

        _ => UuidV5.CreateInventario(
            $"{m.DocumentoTipo}|{companyId}|{m.DocumentoId}|{m.ArticuloBodegaId}")
    };
}
