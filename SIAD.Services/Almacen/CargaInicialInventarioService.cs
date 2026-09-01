using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Carga inicial de existencias. Ver <see cref="ICargaInicialInventarioService"/>.
/// </summary>
public sealed class CargaInicialInventarioService : ICargaInicialInventarioService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;
    private readonly IInventarioPostingService _posting;

    /// <summary>Motivo estándar del asiento de apertura (va en la observación).</summary>
    private const string ObservacionLote = "Carga inicial (reconciliación del corte)";
    private const string ObservacionUnitaria = "Carga inicial (alta)";

    public CargaInicialInventarioService(
        SiadDbContext context, ICurrentCompanyService company, IInventarioPostingService posting)
    {
        _context = context;
        _company = company;
        _posting = posting;
    }

    // ── Apertura unitaria (la usan los dos caminos de captura) ───────────────

    public async Task<PosteoResultDto> PostearAperturaAsync(
        int articuloId, int bodegaId, decimal cantidad, decimal costo, string user, CancellationToken ct = default)
    {
        var fila = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId && u.bodega_id == bodegaId)
            .Select(u => u.id)
            .FirstOrDefaultAsync(ct);

        if (fila == 0)
        {
            throw new InvalidOperationException("El artículo no tiene ubicación en esa bodega.");
        }

        // OJO: aquí NO va el gate de apertura cerrada. El cierre del corte prohíbe abrir
        // pares PREEXISTENTES (los que traían existencia del cierre de SIMAFI), no los que
        // nacen después: si se aplicara aquí, cerrar el corte dejaría imposible dar de alta
        // un artículo con existencia para siempre. La distinción no depende de la fecha:
        // el modo CargaInicialNueva exige existencia previa 0, así que un par preexistente
        // no puede colarse por esta vía. El gate sí vive en el lote y en el costo manual.
        return await _posting.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.CargaInicialNueva,
            ArticuloBodegaId = fila,
            Cantidad = cantidad,
            CostoUnitario = costo,
            // Un alta de hoy se fecha hoy: fecharla en el corte sería un anacronismo contable.
            Fecha = DateOnly.FromDateTime(DateTime.Today),
            DocumentoTipo = TipoDocumentoInventario.CargaInicial,
            DocumentoId = fila,
            Observacion = ObservacionUnitaria,
            Intento = await SiguienteIntentoAsync(articuloId, bodegaId, ct)
        }, user, ct);
    }

    // ── Universo del corte ───────────────────────────────────────────────────

    public async Task<IReadOnlyList<CargaInicialPendienteDto>> GetPendientesAsync(int? bodegaId = null, CancellationToken ct = default)
        => await ConsultaUniversoAsync(bodegaId, ct);

    public async Task<CargaInicialSimulacionDto> SimularLoteAsync(int? bodegaId = null, CancellationToken ct = default)
    {
        var universo = await ConsultaUniversoAsync(bodegaId, ct);

        return new CargaInicialSimulacionDto
        {
            TotalPares = universo.Count,
            Posteables = universo.Count(p => p.Clase == ClaseePendienteCarga.Posteable),
            SinCosto = universo.Count(p => p.Clase == ClaseePendienteCarga.SinCosto),
            Negativas = universo.Count(p => p.Clase == ClaseePendienteCarga.Negativa),
            ArticulosDescontinuados = universo.Count(p => p.Clase == ClaseePendienteCarga.ArticuloDescontinuado),
            BodegasInactivas = universo.Count(p => p.Clase == ClaseePendienteCarga.BodegaInactiva),
            ValorTotalASembrar = universo
                .Where(p => p.Clase == ClaseePendienteCarga.Posteable)
                .Sum(p => p.ValorApertura),
            ArticulosAfectados = universo.Select(p => p.ArticuloId).Distinct().Count()
        };
    }

    // ── Lote retroactivo ─────────────────────────────────────────────────────

    public async Task<CargaInicialResultadoDto> EjecutarLoteAsync(
        DateOnly fechaCorte, int tamanoLote, string user, int? bodegaId = null, CancellationToken ct = default)
    {
        if (tamanoLote <= 0)
        {
            tamanoLote = 200;
        }

        await ExigirAperturaNoCerradaAsync(ct);

        var universo = await ConsultaUniversoAsync(bodegaId, ct);
        var posteables = universo
            .Where(p => p.Clase == ClaseePendienteCarga.Posteable)
            .Take(tamanoLote)
            .ToList();

        var resultado = new CargaInicialResultadoDto();
        var posteadas = 0;
        var omitidas = 0;

        foreach (var par in posteables)
        {
            try
            {
                await _posting.PostearAsync(new MovimientoInventarioDto
                {
                    Tipo = TipoMovimientoInventario.CargaInicialReconciliacion,
                    ArticuloBodegaId = par.ArticuloBodegaId,
                    // En reconciliación la cantidad ES la existencia registrada: el motor
                    // la vuelve a leer bajo bloqueo y rechaza si no coincide.
                    Cantidad = par.Existencia,
                    CostoUnitario = par.CostoApertura,
                    Fecha = fechaCorte,
                    DocumentoTipo = TipoDocumentoInventario.CargaInicial,
                    DocumentoId = par.ArticuloBodegaId,
                    Observacion = ObservacionLote,
                    Intento = await SiguienteIntentoAsync(par.ArticuloId, par.BodegaId, ct)
                }, user, ct);

                posteadas++;
            }
            catch (DbUpdateConcurrencyException)
            {
                // Otro proceso tocó la fila: el lote es idempotente y reanudable, así que
                // se omite y la siguiente corrida la toma. No se aborta el lote entero.
                omitidas++;
                resultado.Detalle.Add(new CargaInicialOmisionDto
                {
                    ArticuloBodegaId = par.ArticuloBodegaId,
                    ArticuloCodigo = par.ArticuloCodigo,
                    Motivo = "Omitida por concurrencia: la reprocesa la siguiente corrida."
                });
            }
            catch (InvalidOperationException ex)
            {
                omitidas++;
                resultado.Detalle.Add(new CargaInicialOmisionDto
                {
                    ArticuloBodegaId = par.ArticuloBodegaId,
                    ArticuloCodigo = par.ArticuloCodigo,
                    Motivo = ex.Message
                });
            }
        }

        // La fecha de corte se persiste al ejecutar: es el parámetro del que depende la
        // fecha contable de TODOS los asientos del lote.
        await GuardarFechaCorteAsync(fechaCorte, user, ct);

        return new CargaInicialResultadoDto
        {
            Posteadas = posteadas,
            Omitidas = omitidas,
            Detalle = resultado.Detalle
        };
    }

    public async Task<CargaInicialResultadoDto> PostearConCostoManualAsync(
        IReadOnlyList<CargaInicialCostoManualDto> items, DateOnly fechaCorte, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(items);
        await ExigirAperturaNoCerradaAsync(ct);

        var resultado = new CargaInicialResultadoDto();
        var posteadas = 0;
        var omitidas = 0;

        foreach (var item in items)
        {
            if (item.Costo <= 0m)
            {
                omitidas++;
                resultado.Detalle.Add(new CargaInicialOmisionDto
                {
                    Motivo = $"El costo capturado para el artículo {item.ArticuloId} debe ser mayor que cero."
                });
                continue;
            }

            var fila = await _context.alm_articulo_bodegas.AsNoTracking()
                .Where(u => u.articulo_id == item.ArticuloId && u.bodega_id == item.BodegaId)
                .Select(u => new { u.id, u.existencia })
                .FirstOrDefaultAsync(ct);

            if (fila is null)
            {
                omitidas++;
                resultado.Detalle.Add(new CargaInicialOmisionDto
                {
                    Motivo = $"El artículo {item.ArticuloId} no tiene ubicación en la bodega {item.BodegaId}."
                });
                continue;
            }

            try
            {
                await _posting.PostearAsync(new MovimientoInventarioDto
                {
                    Tipo = TipoMovimientoInventario.CargaInicialReconciliacion,
                    ArticuloBodegaId = fila.id,
                    Cantidad = fila.existencia,
                    CostoUnitario = item.Costo,
                    Fecha = fechaCorte,
                    DocumentoTipo = TipoDocumentoInventario.CargaInicial,
                    DocumentoId = fila.id,
                    // El costo tecleado NO se guarda en ninguna tabla intermedia: queda
                    // en el asiento, que es el registro. Se deja constancia del origen.
                    Observacion = "Carga inicial (costo capturado a mano)",
                    Intento = await SiguienteIntentoAsync(item.ArticuloId, item.BodegaId, ct)
                }, user, ct);

                posteadas++;
            }
            catch (InvalidOperationException ex)
            {
                omitidas++;
                resultado.Detalle.Add(new CargaInicialOmisionDto
                {
                    ArticuloBodegaId = fila.id,
                    Motivo = ex.Message
                });
            }
        }

        return new CargaInicialResultadoDto
        {
            Posteadas = posteadas,
            Omitidas = omitidas,
            Detalle = resultado.Detalle
        };
    }

    // ── Reapertura ───────────────────────────────────────────────────────────

    public async Task<PosteoResultDto> ReabrirAsync(
        int articuloId, int bodegaId, decimal nuevoCosto, string motivo, string user, CancellationToken ct = default)
    {
        if (nuevoCosto <= 0m)
        {
            throw new InvalidOperationException("El costo de reapertura debe ser mayor que cero.");
        }
        if (string.IsNullOrWhiteSpace(motivo))
        {
            throw new InvalidOperationException("La reapertura exige un motivo: queda en el kardex como auditoría.");
        }

        var fila = await _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId && u.bodega_id == bodegaId)
            .Select(u => u.id)
            .FirstOrDefaultAsync(ct);

        if (fila == 0)
        {
            throw new InvalidOperationException("El artículo no tiene ubicación en esa bodega.");
        }

        // Apertura vigente del par: la de menor (fecha, id) sin reversa.
        var apertura = await AperturaVigenteAsync(articuloId, bodegaId, ct)
            ?? throw new InvalidOperationException(
                "La ubicación no tiene una carga inicial vigente que reabrir.");

        // Con movimientos POSTERIORES a la apertura la corrección no es reapertura: sería
        // revertir el punto cero dejando colgando todo lo que vino después.
        var tienePosteriores = await _context.alm_kardexs.AsNoTracking()
            .AnyAsync(k => k.articulo_id == articuloId
                        && k.bodega_id == bodegaId
                        && k.uuid != null
                        && k.id != apertura.Id
                        && (k.fecha > apertura.Fecha || (k.fecha == apertura.Fecha && k.id > apertura.Id)), ct);

        if (tienePosteriores)
        {
            throw new InvalidOperationException(
                "La bodega ya tiene movimientos posteriores a la apertura. Corrija el costo con un ajuste de inventario (clase VALOR).");
        }

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // 1) Reversa de la apertura mal costeada.
        await _posting.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.Reversa,
            ArticuloBodegaId = fila,
            Cantidad = apertura.Ingresos,
            CostoUnitario = apertura.ValorUnitario,
            // Misma fecha del asiento revertido: el corte es un punto cero y moverlo
            // rompería el orden del libro. La fecha real queda en fechacreacion.
            Fecha = apertura.Fecha,
            DocumentoTipo = TipoDocumentoInventario.Reversa,
            DocumentoId = apertura.Id,
            KardexIdRevertido = apertura.Id,
            Observacion = $"Reapertura: {motivo}"
        }, user, ct);

        // 2) Nueva apertura con el costo correcto. Ya hay una revertida, así que
        //    intento = 2 y el uuid es distinto: el índice único no estorba.
        var resultado = await _posting.PostearAsync(new MovimientoInventarioDto
        {
            Tipo = TipoMovimientoInventario.CargaInicialNueva,
            ArticuloBodegaId = fila,
            Cantidad = apertura.Ingresos,
            CostoUnitario = nuevoCosto,
            Fecha = apertura.Fecha,
            DocumentoTipo = TipoDocumentoInventario.CargaInicial,
            DocumentoId = fila,
            Observacion = $"Reapertura: {motivo}",
            Intento = await SiguienteIntentoAsync(articuloId, bodegaId, ct)
        }, user, ct);

        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return resultado;
    }

    // ── Cierre y configuración ───────────────────────────────────────────────

    public async Task CerrarAperturaAsync(string user, CancellationToken ct = default)
    {
        // El gate del cierre es GLOBAL a propósito: no se cierra el corte de la empresa
        // porque una bodega esté lista, sino cuando no queda nada pendiente en ninguna.
        var simulacion = await SimularLoteAsync(bodegaId: null, ct);

        if (!simulacion.PuedeCerrarse)
        {
            throw new InvalidOperationException(
                $"No se puede cerrar el corte: quedan {simulacion.SinCosto} par(es) sin costo y {simulacion.Negativas} con existencia negativa. Resuélvalos con costo manual o con un ajuste.");
        }

        if (simulacion.Posteables > 0)
        {
            throw new InvalidOperationException(
                $"No se puede cerrar el corte: quedan {simulacion.Posteables} par(es) sin apertura posteada. Ejecute el lote primero.");
        }

        var config = await ObtenerOCrearConfigAsync(ct);
        config.apertura_cerrada = true;
        config.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        config.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<CargaInicialConfigDto> GetConfigAsync(CancellationToken ct = default)
    {
        var config = await ObtenerOCrearConfigAsync(ct);
        return new CargaInicialConfigDto
        {
            BaseCostoApertura = config.base_costo_apertura,
            CostoAperturaIncluyeIsv = config.costo_apertura_incluye_isv,
            FechaCorteApertura = config.fecha_corte_apertura,
            AperturaCerrada = config.apertura_cerrada
        };
    }

    // ── Piezas internas ──────────────────────────────────────────────────────

    /// <summary>
    /// Pares con existencia distinta de 0 y SIN apertura vigente, ya clasificados.
    /// Incluye a propósito las bodegas inactivas y los artículos descontinuados: existencia
    /// sin respaldo es existencia sin respaldo, y hay que verla para decidir qué hacer.
    /// </summary>
    private async Task<List<CargaInicialPendienteDto>> ConsultaUniversoAsync(int? bodegaId, CancellationToken ct)
    {
        var query = _context.alm_articulo_bodegas.AsNoTracking().Where(u => u.existencia != 0m);

        if (bodegaId.HasValue)
        {
            query = query.Where(u => u.bodega_id == bodegaId.Value);
        }

        var candidatos = await query
            .Select(u => new
            {
                ArticuloBodegaId = u.id,
                u.articulo_id,
                u.bodega_id,
                u.existencia,
                UbicacionActiva = u.activo,
                ArticuloCodigo = u.articulo != null ? u.articulo.codigo_articulo : string.Empty,
                ArticuloDescripcion = u.articulo != null ? u.articulo.descripcion : string.Empty,
                ArticuloActivo = u.articulo != null && u.articulo.activo,
                ValorUnitario = u.articulo != null ? u.articulo.valor_unitario : 0m,
                BodegaCodigo = u.bodega != null ? u.bodega.codigo : string.Empty,
                BodegaNombre = u.bodega != null ? u.bodega.nombre : string.Empty
            })
            .ToListAsync(ct);

        if (candidatos.Count == 0)
        {
            return new List<CargaInicialPendienteDto>();
        }

        // Pares que YA tienen apertura vigente: se excluyen del universo.
        var articuloIds = candidatos.Select(c => c.articulo_id).Distinct().ToList();

        var aperturas = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.documento_tipo == TipoDocumentoInventario.CargaInicial
                     && k.articulo_id != null
                     && articuloIds.Contains(k.articulo_id.Value))
            .Select(k => new { k.id, k.articulo_id, k.bodega_id })
            .ToListAsync(ct);

        var revertidos = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.documento_tipo == TipoDocumentoInventario.Reversa && k.documento_id != null)
            .Select(k => k.documento_id!.Value)
            .ToListAsync(ct);

        var revertidosSet = revertidos.ToHashSet();
        var conAperturaVigente = aperturas
            .Where(a => !revertidosSet.Contains(a.id))
            .Select(a => (a.articulo_id, a.bodega_id))
            .ToHashSet();

        return candidatos
            .Where(c => !conAperturaVigente.Contains((c.articulo_id, c.bodega_id)))
            .Select(c => new CargaInicialPendienteDto
            {
                ArticuloBodegaId = c.ArticuloBodegaId,
                ArticuloId = c.articulo_id,
                BodegaId = c.bodega_id,
                ArticuloCodigo = c.ArticuloCodigo,
                ArticuloDescripcion = c.ArticuloDescripcion,
                BodegaCodigo = c.BodegaCodigo,
                BodegaNombre = c.BodegaNombre,
                Existencia = c.existencia,
                CostoApertura = c.ValorUnitario,
                Clase = Clasificar(c.existencia, c.ValorUnitario, c.ArticuloActivo, c.UbicacionActiva)
            })
            .OrderBy(p => p.ArticuloCodigo)
            .ThenBy(p => p.BodegaCodigo)
            .ToList();
    }

    /// <summary>
    /// Orden de clasificación deliberado: primero lo que IMPIDE postear (negativa, sin
    /// costo), luego el contexto (descontinuado, bodega inactiva). Una fila negativa Y sin
    /// costo se reporta como negativa: es el problema que hay que resolver primero.
    /// </summary>
    private static string Clasificar(decimal existencia, decimal valorUnitario, bool articuloActivo, bool ubicacionActiva)
    {
        if (existencia < 0m) return ClaseePendienteCarga.Negativa;
        if (valorUnitario <= 0m) return ClaseePendienteCarga.SinCosto;
        if (!articuloActivo) return ClaseePendienteCarga.ArticuloDescontinuado;
        if (!ubicacionActiva) return ClaseePendienteCarga.BodegaInactiva;
        return ClaseePendienteCarga.Posteable;
    }

    private sealed record AperturaVigente(int Id, DateOnly Fecha, decimal Ingresos, decimal ValorUnitario);

    private async Task<AperturaVigente?> AperturaVigenteAsync(int articuloId, int bodegaId, CancellationToken ct)
    {
        var aperturas = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId
                     && k.bodega_id == bodegaId
                     && k.documento_tipo == TipoDocumentoInventario.CargaInicial)
            .Select(k => new { k.id, k.fecha, k.ingresos, k.valor_unitario })
            .ToListAsync(ct);

        if (aperturas.Count == 0)
        {
            return null;
        }

        var ids = aperturas.Select(a => a.id).ToList();
        var revertidos = (await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.documento_tipo == TipoDocumentoInventario.Reversa
                     && k.documento_id != null
                     && ids.Contains(k.documento_id.Value))
            .Select(k => k.documento_id!.Value)
            .ToListAsync(ct)).ToHashSet();

        var vigente = aperturas
            .Where(a => !revertidos.Contains(a.id))
            .OrderBy(a => a.fecha)
            .ThenBy(a => a.id)
            .FirstOrDefault();

        return vigente is null
            ? null
            : new AperturaVigente(vigente.id, vigente.fecha ?? DateOnly.FromDateTime(DateTime.Today),
                                  vigente.ingresos, vigente.valor_unitario);
    }

    /// <summary>1 + aperturas del par ya revertidas: el discriminador del uuid.</summary>
    private async Task<int> SiguienteIntentoAsync(int articuloId, int bodegaId, CancellationToken ct)
    {
        var aperturas = await _context.alm_kardexs.AsNoTracking()
            .Where(k => k.articulo_id == articuloId
                     && k.bodega_id == bodegaId
                     && k.documento_tipo == TipoDocumentoInventario.CargaInicial)
            .Select(k => k.id)
            .ToListAsync(ct);

        if (aperturas.Count == 0)
        {
            return 1;
        }

        var revertidas = await _context.alm_kardexs.AsNoTracking()
            .CountAsync(k => k.documento_tipo == TipoDocumentoInventario.Reversa
                          && k.documento_id != null
                          && aperturas.Contains(k.documento_id.Value), ct);

        return revertidas + 1;
    }

    private async Task ExigirAperturaNoCerradaAsync(CancellationToken ct)
    {
        var config = await ObtenerOCrearConfigAsync(ct);
        if (config.apertura_cerrada)
        {
            throw new InvalidOperationException(
                "El corte de inventario está cerrado: no se aceptan aperturas nuevas para pares preexistentes. Use reabrir (permiso de Configuración) o un ajuste.");
        }
    }

    private async Task GuardarFechaCorteAsync(DateOnly fechaCorte, string user, CancellationToken ct)
    {
        var config = await ObtenerOCrearConfigAsync(ct);
        config.fecha_corte_apertura = fechaCorte;
        config.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        config.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
    }

    /// <summary>
    /// La fila de configuración de la empresa. El script la siembra, pero una empresa
    /// creada después no la tendría: se crea al vuelo con los valores por defecto.
    /// </summary>
    private async Task<alm_config_inventario> ObtenerOCrearConfigAsync(CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        var config = await _context.alm_config_inventarios.FirstOrDefaultAsync(ct);

        if (config is not null)
        {
            return config;
        }

        config = new alm_config_inventario
        {
            company_id = companyId,
            base_costo_apertura = "VALOR_UNITARIO",
            // valor_unitario NO lleva ISV (decisión del contador, 2026-07-30).
            costo_apertura_incluye_isv = false,
            apertura_cerrada = false,
            usuariocreacion = "system",
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.alm_config_inventarios.Add(config);
        await _context.SaveChangesAsync(ct);
        return config;
    }
}
