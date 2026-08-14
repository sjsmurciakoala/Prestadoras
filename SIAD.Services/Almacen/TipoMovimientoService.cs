using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Mantenimiento del catálogo de tipos de movimiento de almacén. Ver
/// <see cref="ITipoMovimientoService"/>.
/// <para>
/// El filtro global de empresa (<c>SiadDbContext.Tenancy</c>) acota toda consulta y estampa el
/// <c>company_id</c> en el INSERT: la unicidad del código y la resolución por id ya son por
/// empresa, no hay que filtrar a mano.
/// </para>
/// </summary>
public sealed class TipoMovimientoService : ITipoMovimientoService
{
    private readonly SiadDbContext _context;

    public TipoMovimientoService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<TipoMovimientoAlmacenListItemDto>> GetAsync(bool soloActivos, CancellationToken ct = default)
    {
        var query = _context.alm_tipo_movimientos.AsNoTracking().AsQueryable();
        if (soloActivos)
        {
            query = query.Where(t => t.activo);
        }

        return await query
            .OrderBy(t => t.orden).ThenBy(t => t.codigo)
            .Select(t => new TipoMovimientoAlmacenListItemDto
            {
                Id = t.id,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Clase = t.clase,
                RequiereAutorizacion = t.requiere_autorizacion,
                NotificaCorreo = t.notifica_correo,
                Activo = t.activo,
                // Derivado de alm_movimiento_dtl desde la Fase 2: si está en uso, la pantalla
                // bloquea el cambio de clase (y el servicio lo rechaza igual del lado servidor).
                EnUso = _context.alm_movimiento_dtls.Any(
                    d => d.posteado && d.cabecera.tipo_movimiento_id == t.id)
            })
            .ToListAsync(ct);
    }

    public async Task<TipoMovimientoAlmacenDto?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;
        return await _context.alm_tipo_movimientos.AsNoTracking()
            .Where(t => t.id == id)
            .Select(t => new TipoMovimientoAlmacenDto
            {
                Id = t.id,
                Codigo = t.codigo,
                Nombre = t.nombre,
                Clase = t.clase,
                RequiereAutorizacion = t.requiere_autorizacion,
                NotificaCorreo = t.notifica_correo,
                CuentaContable = t.cuenta_contable,
                Activo = t.activo,
                Orden = t.orden
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<TipoMovimientoAlmacenDto> CrearAsync(TipoMovimientoAlmacenDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 20, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 80, "nombre");
        var clase = NormalizarClase(dto.Clase);

        if (await _context.alm_tipo_movimientos.AsNoTracking().AnyAsync(t => t.codigo == codigo, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de movimiento con el código {codigo}.");
        }

        var entity = new alm_tipo_movimiento
        {
            codigo = codigo,
            nombre = nombre,
            clase = clase,
            requiere_autorizacion = dto.RequiereAutorizacion,
            notifica_correo = dto.NotificaCorreo,
            cuenta_contable = ClasificacionNormalizer.Opcional(dto.CuentaContable, 20),
            activo = dto.Activo,
            orden = dto.Orden,
            usuariocreacion = ClasificacionNormalizer.Usuario(user),
            fechacreacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        };

        _context.alm_tipo_movimientos.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        dto.Codigo = codigo;
        dto.Nombre = nombre;
        dto.Clase = clase;
        return dto;
    }

    public async Task<TipoMovimientoAlmacenDto> ActualizarAsync(int id, TipoMovimientoAlmacenDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.alm_tipo_movimientos.FirstOrDefaultAsync(t => t.id == id, ct)
                     ?? throw new KeyNotFoundException("El tipo de movimiento no existe.");

        var codigo = ClasificacionNormalizer.Requerido(dto.Codigo, 20, "código", mayus: true);
        var nombre = ClasificacionNormalizer.Requerido(dto.Nombre, 80, "nombre");
        var clase = NormalizarClase(dto.Clase);

        if (await _context.alm_tipo_movimientos.AsNoTracking().AnyAsync(t => t.codigo == codigo && t.id != id, ct))
        {
            throw new InvalidOperationException($"Ya existe un tipo de movimiento con el código {codigo}.");
        }

        if (clase != entity.clase && await TieneMovimientosPosteadosAsync(id, ct))
        {
            throw new InvalidOperationException(
                "No se puede cambiar la clase de un tipo que ya tiene movimientos posteados: " +
                "reinterpretaría los asientos ya registrados. Cree un tipo nuevo.");
        }

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.clase = clase;
        entity.requiere_autorizacion = dto.RequiereAutorizacion;
        entity.notifica_correo = dto.NotificaCorreo;
        entity.cuenta_contable = ClasificacionNormalizer.Opcional(dto.CuentaContable, 20);
        entity.activo = dto.Activo;
        entity.orden = dto.Orden;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        dto.Codigo = codigo;
        dto.Nombre = nombre;
        dto.Clase = clase;
        return dto;
    }

    public async Task<bool> DesactivarAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_tipo_movimientos.FirstOrDefaultAsync(t => t.id == id, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        entity.activo = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    private static string NormalizarClase(string clase)
    {
        var normalizada = ClasificacionNormalizer.Requerido(clase, 10, "clase", mayus: true);
        // Vocabulario del catálogo de movimientos (incluye TRASLADO); NO ClaseAjusteInventario,
        // que es el del ajuste de una línea y se queda en ENTRADA/SALIDA/VALOR.
        if (!ClaseMovimientoInventario.EsValida(normalizada))
        {
            throw new InvalidOperationException(
                $"La clase '{clase}' no es válida. Use {string.Join(", ", ClaseMovimientoInventario.Todas)}.");
        }
        return normalizada;
    }

    /// <summary>
    /// ¿El tipo ya generó asientos posteados? Conectado a <c>alm_movimiento_dtl</c> desde la
    /// Fase 2 (2026-08-03). Si hay aunque sea un renglón posteado, la <c>clase</c> queda
    /// congelada: cambiarla reinterpretaría retroactivamente los asientos ya registrados.
    /// </summary>
    private Task<bool> TieneMovimientosPosteadosAsync(int tipoId, CancellationToken ct)
        => _context.alm_movimiento_dtls.AsNoTracking()
            .AnyAsync(d => d.posteado && d.cabecera.tipo_movimiento_id == tipoId, ct);
}
