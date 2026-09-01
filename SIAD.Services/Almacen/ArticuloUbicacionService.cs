using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Ubicaciones físicas de un artículo por bodega (bodega + ubicación manual + principal).
/// La ubicación es texto libre: cinco campos de 20 caracteres (ubicacion1..5).
/// Las ubicaciones no se eliminan: se DESHABILITAN (activo=false) para conservar el
/// histórico. El rollup de existencia del artículo suma solo las filas activas, así que
/// una bodega solo puede deshabilitarse cuando ya quedó en cero (ver DeshabilitarAsync).
/// Toda operación de escritura es ATÓMICA (fila de bodega + recompute de cabecera): si
/// se partiera en dos commits, un fallo intermedio dejaría la cabecera descuadrada.
/// Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// <para>
/// <b>La existencia ya no se teclea.</b> Al ALTA del par se postea como carga inicial en
/// el kardex (con su costo); después solo la mueven documentos de inventario. Ni
/// <c>AddAsync</c> en su rama de reactivación ni <c>UpdateAsync</c> la escriben desde el
/// DTO: si lo hicieran, la apertura caducaría con la primera edición.
/// </para>
/// </summary>
public sealed class ArticuloUbicacionService : IArticuloUbicacionService
{
    private readonly SiadDbContext _context;
    private readonly IArticuloRollupService _rollup;
    private readonly ICargaInicialInventarioService _carga;
    private readonly IInventarioPostingService _posting;

    public ArticuloUbicacionService(
        SiadDbContext context,
        IArticuloRollupService rollup,
        ICargaInicialInventarioService carga,
        IInventarioPostingService posting)
    {
        _context = context;
        _rollup = rollup;
        _carga = carga;
        _posting = posting;
    }

    public async Task<IReadOnlyList<ArticuloUbicacionDto>> GetAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        if (articuloId <= 0)
        {
            return Array.Empty<ArticuloUbicacionDto>();
        }

        var query = _context.alm_articulo_bodegas.AsNoTracking()
            .Where(u => u.articulo_id == articuloId);

        if (!incluirInactivas)
        {
            query = query.Where(u => u.activo);
        }

        return await query
            .OrderByDescending(u => u.activo)
            .ThenByDescending(u => u.principal)
            .ThenBy(u => u.bodega != null ? u.bodega.codigo : string.Empty)
            .Select(u => new ArticuloUbicacionDto
            {
                Id = u.id,
                BodegaId = u.bodega_id,
                BodegaDisplay = u.bodega != null ? u.bodega.codigo + " — " + u.bodega.nombre : null,
                Ubicacion1 = u.ubicacion1,
                Ubicacion2 = u.ubicacion2,
                Ubicacion3 = u.ubicacion3,
                Ubicacion4 = u.ubicacion4,
                Ubicacion5 = u.ubicacion5,
                Existencia = u.existencia,
                ExistenciaMinima = u.existencia_minima,
                ExistenciaMaxima = u.existencia_maxima,
                PuntoReorden = u.punto_reorden,
                // Campos del motor de movimientos: se leen para mostrarlos, nunca se escriben desde el DTO.
                ExistenciaComprometida = u.existencia_comprometida,
                ExistenciaTransito = u.existencia_transito,
                CostoPromedio = u.costo_promedio,
                UltimoCosto = u.ultimo_costo,
                Principal = u.principal,
                Activo = u.activo
            })
            .ToListAsync(ct);
    }

    public async Task<ArticuloUbicacionDto> AddAsync(int articuloId, ArticuloUbicacionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarArticuloAsync(articuloId, ct);
        await ValidarBodegaAsync(dto.BodegaId, ct);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        // Si ya existe una fila para esa bodega: si está activa es un duplicado;
        // si está deshabilitada, se reactiva (respeta el único (company, articulo, bodega)).
        var existente = await _context.alm_articulo_bodegas
            .FirstOrDefaultAsync(u => u.articulo_id == articuloId && u.bodega_id == dto.BodegaId, ct);

        if (existente is not null && existente.activo)
        {
            throw new InvalidOperationException("El artículo ya tiene una ubicación activa en esa bodega.");
        }

        // Reactivación: devolver la fila al rollup con existencia sin asiento que la respalde
        // es exactamente el descuadre que persigue este módulo. Se comprueba ANTES de tocar
        // nada (desmarcar la principal ya escribe) para que el rechazo no deje rastro.
        if (existente is not null)
        {
            await ExigirRespaldoParaReactivarAsync(existente, ct);
        }
        else
        {
            // Apertura: la cantidad tecleada al alta del par se postea al kardex, no se
            // escribe a mano. Se valida antes de escribir nada para no dejar rastro si falta
            // el costo. (En una reactivación la existencia del DTO se ignora: no aplica.)
            ValidarCostoApertura(dto.Existencia, dto.CostoApertura);
        }

        if (dto.Principal)
        {
            await DesmarcarPrincipalAsync(articuloId, existente?.id, ct);
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var usuario = ClasificacionNormalizer.Usuario(user);

        if (existente is not null)
        {
            existente.activo = true;
            existente.ubicacion1 = ClasificacionNormalizer.Opcional(dto.Ubicacion1, 20);
            existente.ubicacion2 = ClasificacionNormalizer.Opcional(dto.Ubicacion2, 20);
            existente.ubicacion3 = ClasificacionNormalizer.Opcional(dto.Ubicacion3, 20);
            existente.ubicacion4 = ClasificacionNormalizer.Opcional(dto.Ubicacion4, 20);
            existente.ubicacion5 = ClasificacionNormalizer.Opcional(dto.Ubicacion5, 20);
            existente.existencia_minima = dto.ExistenciaMinima;
            existente.existencia_maxima = dto.ExistenciaMaxima;
            existente.punto_reorden = dto.PuntoReorden;
            // existencia, existencia_comprometida, existencia_transito, costo_promedio y
            // ultimo_costo NO se escriben desde el DTO: los mantiene el motor de posteo. Se
            // conserva lo que ya tiene la fila (una fila deshabilitada viene en 0 porque
            // DeshabilitarAsync lo exige; si trae saldo, la guarda de arriba ya frenó).
            existente.principal = dto.Principal;
            existente.usuariomodificacion = usuario;
            existente.fechamodificacion = ahora;
            await _context.SaveChangesAsync(ct);
            await _rollup.RecomputeAsync(articuloId, ct);
            await TransaccionAmbiente.ConfirmarAsync(tx, ct);
            dto.Id = existente.id;
            dto.Activo = true;
            CopiarCamposDelMotor(existente, dto);
            return dto;
        }

        var aperturaCantidad = dto.Existencia;

        var entity = new alm_articulo_bodega
        {
            articulo_id = articuloId,
            bodega_id = dto.BodegaId,
            ubicacion1 = ClasificacionNormalizer.Opcional(dto.Ubicacion1, 20),
            ubicacion2 = ClasificacionNormalizer.Opcional(dto.Ubicacion2, 20),
            ubicacion3 = ClasificacionNormalizer.Opcional(dto.Ubicacion3, 20),
            ubicacion4 = ClasificacionNormalizer.Opcional(dto.Ubicacion4, 20),
            ubicacion5 = ClasificacionNormalizer.Opcional(dto.Ubicacion5, 20),
            // Nace en CERO: la existencia entra por el asiento de carga inicial de abajo,
            // dentro de esta misma transacción.
            existencia = 0m,
            existencia_minima = dto.ExistenciaMinima,
            existencia_maxima = dto.ExistenciaMaxima,
            punto_reorden = dto.PuntoReorden,
            // Los 4 campos del motor (comprometida, tránsito, costo promedio, último costo)
            // nacen en 0 por DEFAULT y sólo los mueve el motor de posteo: no se toman del DTO.
            principal = dto.Principal,
            activo = true,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        _context.alm_articulo_bodegas.Add(entity);
        await _context.SaveChangesAsync(ct);

        if (aperturaCantidad > 0m)
        {
            await _carga.PostearAperturaAsync(articuloId, dto.BodegaId, aperturaCantidad, dto.CostoApertura, user, ct);
            // El posteo ya movió la fila y la cabecera: se refresca para devolver la verdad.
            await _context.Entry(entity).ReloadAsync(ct);
        }

        await _rollup.RecomputeAsync(articuloId, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        dto.Id = entity.id;
        dto.Activo = true;
        CopiarCamposDelMotor(entity, dto);
        return dto;
    }

    public async Task<ArticuloUbicacionDto> UpdateAsync(int articuloId, int id, ArticuloUbicacionDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        await ValidarArticuloAsync(articuloId, ct);

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct)
                     ?? throw new KeyNotFoundException("La ubicación no existe.");

        if (!entity.activo)
        {
            throw new InvalidOperationException("No se puede editar una ubicación deshabilitada. Reactívela primero.");
        }

        await ValidarBodegaAsync(dto.BodegaId, ct);

        if (await _context.alm_articulo_bodegas.AsNoTracking()
                .AnyAsync(u => u.articulo_id == articuloId && u.bodega_id == dto.BodegaId && u.id != id, ct))
        {
            throw new InvalidOperationException("El artículo ya tiene una ubicación en esa bodega.");
        }

        if (dto.Principal)
        {
            await DesmarcarPrincipalAsync(articuloId, id, ct);
        }

        entity.bodega_id = dto.BodegaId;
        entity.ubicacion1 = ClasificacionNormalizer.Opcional(dto.Ubicacion1, 20);
        entity.ubicacion2 = ClasificacionNormalizer.Opcional(dto.Ubicacion2, 20);
        entity.ubicacion3 = ClasificacionNormalizer.Opcional(dto.Ubicacion3, 20);
        entity.ubicacion4 = ClasificacionNormalizer.Opcional(dto.Ubicacion4, 20);
        entity.ubicacion5 = ClasificacionNormalizer.Opcional(dto.Ubicacion5, 20);
        entity.existencia_minima = dto.ExistenciaMinima;
        entity.existencia_maxima = dto.ExistenciaMaxima;
        entity.punto_reorden = dto.PuntoReorden;
        // existencia, existencia_comprometida, existencia_transito, costo_promedio y
        // ultimo_costo NO se escriben desde el DTO (aunque el cliente los mande): son del
        // motor de posteo. Sin esto, la carga inicial caducaría con la primera edición.
        entity.principal = dto.Principal;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await _rollup.RecomputeAsync(articuloId, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        dto.Id = entity.id;
        dto.Activo = true;
        CopiarCamposDelMotor(entity, dto);
        return dto;
    }

    /// <summary>
    /// Devuelve al cliente los campos del motor con el valor REAL de la fila, para que la
    /// respuesta no le refleje de vuelta lo que él haya mandado (que se ignora al escribir).
    /// Incluye la existencia: al alta es el resultado del asiento de apertura, y en
    /// edición/reactivación es lo que ya había.
    /// </summary>
    private static void CopiarCamposDelMotor(alm_articulo_bodega entity, ArticuloUbicacionDto dto)
    {
        dto.Existencia = entity.existencia;
        dto.ExistenciaComprometida = entity.existencia_comprometida;
        dto.ExistenciaTransito = entity.existencia_transito;
        dto.CostoPromedio = entity.costo_promedio;
        dto.UltimoCosto = entity.ultimo_costo;
    }

    /// <summary>
    /// Deshabilita (soft-delete) la ubicación. EXIGE que la bodega esté en cero: deshabilitar
    /// una fila con stock la saca del rollup de cabecera sin generar ningún movimiento de
    /// kardex, y ese es el generador más común del descuadre que reporta el filtro
    /// "Con descuadre" del maestro. La salida del stock debe registrarse antes (traslado a
    /// otra bodega o ajuste de inventario), y solo entonces se deshabilita la ubicación.
    /// </summary>
    public async Task<bool> DeshabilitarAsync(int articuloId, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        if (entity.principal)
        {
            throw new InvalidOperationException("No se puede deshabilitar la bodega principal. Marque otra bodega como principal primero.");
        }

        var otrasActivas = await _context.alm_articulo_bodegas.AsNoTracking()
            .CountAsync(u => u.articulo_id == articuloId && u.activo && u.id != id, ct);
        if (otrasActivas == 0)
        {
            throw new InvalidOperationException("El artículo debe conservar al menos una bodega activa.");
        }

        // Stock remanente (positivo o negativo): se bloquea. Sin esta guarda la existencia
        // desaparecería del total del artículo sin rastro en el kardex.
        if (entity.existencia != 0m)
        {
            throw new InvalidOperationException(
                $"No se puede deshabilitar la ubicación: la bodega todavía tiene existencia ({Cantidad(entity.existencia)}). " +
                "Traslade el stock a otra bodega o regístrelo con un ajuste de inventario hasta dejarla en 0; " +
                "deshabilitarla con existencia la saca del total del artículo sin movimiento de kardex.");
        }

        // Comprometido / en tránsito: la existencia está en 0 pero hay obligaciones abiertas
        // (requisiciones aprobadas sin despachar, compras o traslados por recibir) que al
        // liquidarse volverían a mover una fila que ya no está en el rollup.
        if (entity.existencia_comprometida != 0m || entity.existencia_transito != 0m)
        {
            throw new InvalidOperationException(
                $"No se puede deshabilitar la ubicación: tiene {Cantidad(entity.existencia_comprometida)} comprometida y " +
                $"{Cantidad(entity.existencia_transito)} en tránsito. Despache o cancele los pendientes primero.");
        }

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await _rollup.RecomputeAsync(articuloId, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    public async Task<bool> ReactivarAsync(int articuloId, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        await using var tx = await TransaccionAmbiente.IniciarAsync(_context, ct);

        var entity = await _context.alm_articulo_bodegas.FirstOrDefaultAsync(u => u.id == id && u.articulo_id == articuloId, ct);
        if (entity is null) return false;
        if (entity.activo) return true;

        if (!await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.id == entity.bodega_id && b.activo, ct))
        {
            throw new InvalidOperationException("No se puede reactivar: la bodega está inactiva.");
        }

        await ExigirRespaldoParaReactivarAsync(entity, ct);

        entity.activo = true;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        await _rollup.RecomputeAsync(articuloId, ct);
        await TransaccionAmbiente.ConfirmarAsync(tx, ct);
        return true;
    }

    /// <summary>Cantidad para mensajes de error: sin ceros de relleno ni notación científica.</summary>
    private static string Cantidad(decimal valor) => valor.ToString("0.####");

    /// <summary>
    /// Simétrica de la guarda de <see cref="DeshabilitarAsync"/>: reactivar devuelve la fila
    /// al rollup de cabecera, así que su existencia vuelve a sumar al total del artículo. Si
    /// esa existencia no tiene un asiento que la respalde, el total crece sin movimiento de
    /// kardex — el mismo descuadre, en la dirección contraria.
    /// <para>
    /// <b>Va en las DOS rutas de reactivación</b> (<see cref="ReactivarAsync"/> y la rama de
    /// <see cref="AddAsync"/> que revive una fila deshabilitada). Hasta la Fase 6 la segunda
    /// quedaba tapada por accidente, porque sobrescribía la existencia con la del DTO;
    /// quitar esa escritura sin poner esta guarda abriría el agujero creyendo cerrarlo.
    /// </para>
    /// </summary>
    private async Task ExigirRespaldoParaReactivarAsync(alm_articulo_bodega fila, CancellationToken ct)
    {
        if (fila.existencia == 0m)
        {
            return;
        }

        if (await _posting.TieneAperturaVigenteAsync(fila.articulo_id, fila.bodega_id, ct))
        {
            return;
        }

        throw new InvalidOperationException(
            $"No se puede reactivar la ubicación: tiene existencia ({Cantidad(fila.existencia)}) sin carga inicial que la respalde. " +
            "Reactivarla devolvería ese saldo al total del artículo sin movimiento de kardex. " +
            "Registre primero la carga inicial del corte, o deje la bodega en 0 con un ajuste de inventario.");
    }

    /// <summary>
    /// La cantidad de apertura del alta exige costo: sin él, el asiento entraría a costo 0 y
    /// corrompería el promedio ponderado de la primera compra que llegue después (y el
    /// kardex es inmutable: no hay UPDATE que lo arregle).
    /// </summary>
    private static void ValidarCostoApertura(decimal cantidad, decimal costo)
    {
        if (cantidad > 0m && costo <= 0m)
        {
            throw new InvalidOperationException(
                "Debe indicar el costo de apertura (mayor que cero) para registrar la existencia inicial de la bodega. " +
                "Si no lo conoce, cree la ubicación en 0 y registre el ingreso con un ajuste de inventario.");
        }
    }

    private async Task ValidarArticuloAsync(int articuloId, CancellationToken ct)
    {
        if (!await _context.alm_articulos.AsNoTracking().AnyAsync(a => a.id == articuloId, ct))
        {
            throw new KeyNotFoundException("El artículo no existe.");
        }
    }

    private async Task ValidarBodegaAsync(int bodegaId, CancellationToken ct)
    {
        if (!await _context.alm_bodegas.AsNoTracking().AnyAsync(b => b.id == bodegaId && b.activo, ct))
        {
            throw new InvalidOperationException("La bodega seleccionada no existe o está inactiva.");
        }
    }

    private async Task DesmarcarPrincipalAsync(int articuloId, int? exceptId, CancellationToken ct)
    {
        var principales = await _context.alm_articulo_bodegas
            .Where(u => u.articulo_id == articuloId && u.principal && (exceptId == null || u.id != exceptId.Value))
            .ToListAsync(ct);

        if (principales.Count == 0)
        {
            return;
        }

        foreach (var p in principales)
        {
            p.principal = false;
        }
        await _context.SaveChangesAsync(ct);
    }
}
