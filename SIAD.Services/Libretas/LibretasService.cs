using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Libretas;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Libretas;

/// <summary>
/// ABM del catálogo global de libretas. El filtro global de tenant de
/// SiadDbContext acota todas las consultas a la empresa actual.
/// </summary>
public sealed class LibretasService : ILibretasService
{
    private readonly SiadDbContext _context;

    public LibretasService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LibretaDto>> ListarAsync(CancellationToken ct = default)
    {
        return await _context.adm_libretas
            .AsNoTracking()
            .OrderBy(l => l.codigo)
            .Select(l => new LibretaDto(l.libreta_id, l.codigo, l.descripcion, l.activo))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<LibretaDto>> ListarActivasAsync(CancellationToken ct = default)
    {
        return await _context.adm_libretas
            .AsNoTracking()
            .Where(l => l.activo)
            .OrderBy(l => l.codigo)
            .Select(l => new LibretaDto(l.libreta_id, l.codigo, l.descripcion, l.activo))
            .ToListAsync(ct);
    }

    public async Task<LibretaDto?> ObtenerAsync(long id, CancellationToken ct = default)
    {
        return await _context.adm_libretas
            .AsNoTracking()
            .Where(l => l.libreta_id == id)
            .Select(l => new LibretaDto(l.libreta_id, l.codigo, l.descripcion, l.activo))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<long> CrearAsync(LibretaUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var codigo = NormalizarCodigo(dto.Codigo);
        await ValidarCodigoUnicoAsync(codigo, excluirId: null, ct);

        var entity = new adm_libreta
        {
            codigo = codigo,
            descripcion = NormalizarDescripcion(dto.Descripcion),
            activo = dto.Activo,
            created_by = NormalizarUsuario(usuario),
            created_at = DateTime.UtcNow,
        };

        _context.adm_libretas.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.libreta_id;
    }

    public async Task ActualizarAsync(long id, LibretaUpsertDto dto, string usuario, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        var entity = await _context.adm_libretas.FirstOrDefaultAsync(l => l.libreta_id == id, ct)
            ?? throw new KeyNotFoundException($"No se encontró la libreta {id}.");

        var codigo = NormalizarCodigo(dto.Codigo);
        if (!string.Equals(entity.codigo, codigo, StringComparison.Ordinal))
        {
            await ValidarCodigoUnicoAsync(codigo, excluirId: id, ct);

            // El código viaja dentro del indicativo de cada cliente; renombrarlo
            // dejaría huérfanos los indicativos existentes.
            var enUso = await _context.cliente_maestros
                .AsNoTracking()
                .AnyAsync(cm => cm.maestro_cliente_indicativo_ruta != null
                                && EF.Functions.ILike(cm.maestro_cliente_indicativo_ruta, "%-" + entity.codigo + "-%"),
                          ct);
            if (enUso)
            {
                throw new ArgumentException(
                    $"La libreta '{entity.codigo}' tiene clientes asignados; no se puede renombrar. Cree una libreta nueva y reasigne los clientes.");
            }

            entity.codigo = codigo;
        }

        entity.descripcion = NormalizarDescripcion(dto.Descripcion);
        entity.activo = dto.Activo;
        entity.updated_by = NormalizarUsuario(usuario);
        entity.updated_at = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> DesactivarAsync(long id, string usuario, CancellationToken ct = default)
    {
        var entity = await _context.adm_libretas.FirstOrDefaultAsync(l => l.libreta_id == id, ct);
        if (entity is null)
        {
            return false;
        }

        if (entity.activo)
        {
            entity.activo = false;
            entity.updated_by = NormalizarUsuario(usuario);
            entity.updated_at = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
        }

        return true;
    }

    public async Task<bool> ExisteActivaAsync(string codigo, CancellationToken ct = default)
    {
        var normalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (normalizado.Length == 0)
        {
            return false;
        }

        return await _context.adm_libretas
            .AsNoTracking()
            .AnyAsync(l => l.activo && l.codigo == normalizado, ct);
    }

    private async Task ValidarCodigoUnicoAsync(string codigo, long? excluirId, CancellationToken ct)
    {
        var duplicado = await _context.adm_libretas
            .AsNoTracking()
            .AnyAsync(l => l.codigo == codigo && (excluirId == null || l.libreta_id != excluirId), ct);
        if (duplicado)
        {
            throw new ArgumentException($"Ya existe una libreta con el código '{codigo}'.");
        }
    }

    private static string NormalizarCodigo(string? codigo)
    {
        var limpio = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (limpio.Length == 0)
        {
            throw new ArgumentException("El código es obligatorio.");
        }

        if (limpio.Length > 10)
        {
            throw new ArgumentException("El código admite máximo 10 caracteres.");
        }

        if (!limpio.All(char.IsLetterOrDigit))
        {
            throw new ArgumentException("El código admite solo letras y números (viaja dentro del indicativo separado por guiones).");
        }

        return limpio;
    }

    private static string? NormalizarDescripcion(string? descripcion)
    {
        var limpio = (descripcion ?? string.Empty).Trim();
        return limpio.Length == 0 ? null : (limpio.Length > 100 ? limpio[..100] : limpio);
    }

    private static string NormalizarUsuario(string? usuario)
        => string.IsNullOrWhiteSpace(usuario) ? "system" : usuario.Trim();
}
