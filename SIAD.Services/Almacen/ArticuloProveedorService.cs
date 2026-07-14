using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Proveedores que suministran un artículo ("UPC"): proveedor + código UPC + costo +
/// principal. Las relaciones no se eliminan: se DESHABILITAN (activo=false) para
/// conservar el histórico. El proveedor se referencia por código y su existencia se
/// valida contra prv_proveedores (keyless/multiempresa); el nombre se resuelve para
/// mostrar. Multiempresa: el filtro y el estampado de company_id los aplica SiadDbContext.
/// </summary>
public sealed class ArticuloProveedorService : IArticuloProveedorService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _company;

    public ArticuloProveedorService(SiadDbContext context, ICurrentCompanyService company)
    {
        _context = context;
        _company = company;
    }

    public async Task<IReadOnlyList<ArticuloProveedorDto>> GetAsync(int articuloId, bool incluirInactivas = false, CancellationToken ct = default)
    {
        if (articuloId <= 0)
        {
            return Array.Empty<ArticuloProveedorDto>();
        }

        var query = _context.alm_articulo_proveedors.AsNoTracking()
            .Where(p => p.articulo_id == articuloId);

        if (!incluirInactivas)
        {
            query = query.Where(p => p.activo);
        }

        var filas = await query
            .OrderByDescending(p => p.activo)
            .ThenByDescending(p => p.principal)
            .ThenBy(p => p.cod_proveedor)
            .Select(p => new ArticuloProveedorDto
            {
                Id = p.id,
                CodProveedor = p.cod_proveedor,
                CodigoUpc = p.codigo_upc,
                Principal = p.principal,
                Activo = p.activo
            })
            .ToListAsync(ct);

        await ResolverNombresAsync(filas, ct);
        return filas;
    }

    public async Task<ArticuloProveedorDto> AddAsync(int articuloId, ArticuloProveedorDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await ValidarArticuloAsync(articuloId, ct);

        var cod = NormalizarCodProveedor(dto.CodProveedor);
        await ValidarProveedorAsync(cod, ct);

        // Si ya existe una fila para ese proveedor: si está activa es duplicado; si está
        // deshabilitada se reactiva (respeta el único (company, articulo, proveedor)).
        var existente = await _context.alm_articulo_proveedors
            .FirstOrDefaultAsync(p => p.articulo_id == articuloId && p.cod_proveedor == cod, ct);

        if (existente is not null && existente.activo)
        {
            throw new InvalidOperationException("El artículo ya tiene ese proveedor asignado.");
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
            existente.codigo_upc = ClasificacionNormalizer.Opcional(dto.CodigoUpc, 40);
            // El costo ya no se maneja por proveedor (se lleva en Existencias): la columna
            // alm_articulo_proveedor.costo sigue en la BD pero no se toca.
            existente.principal = dto.Principal;
            existente.usuariomodificacion = usuario;
            existente.fechamodificacion = ahora;
            await _context.SaveChangesAsync(ct);
            dto.Id = existente.id;
            dto.CodProveedor = cod;
            dto.Activo = true;
            await ResolverNombresAsync(new[] { dto }, ct);
            return dto;
        }

        var entity = new alm_articulo_proveedor
        {
            articulo_id = articuloId,
            cod_proveedor = cod,
            codigo_upc = ClasificacionNormalizer.Opcional(dto.CodigoUpc, 40),
            principal = dto.Principal,
            activo = true,
            usuariocreacion = usuario,
            fechacreacion = ahora
        };
        _context.alm_articulo_proveedors.Add(entity);
        await _context.SaveChangesAsync(ct);
        dto.Id = entity.id;
        dto.CodProveedor = cod;
        dto.Activo = true;
        await ResolverNombresAsync(new[] { dto }, ct);
        return dto;
    }

    public async Task<ArticuloProveedorDto> UpdateAsync(int articuloId, int id, ArticuloProveedorDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        await ValidarArticuloAsync(articuloId, ct);

        var entity = await _context.alm_articulo_proveedors.FirstOrDefaultAsync(p => p.id == id && p.articulo_id == articuloId, ct)
                     ?? throw new KeyNotFoundException("La relación con el proveedor no existe.");

        if (!entity.activo)
        {
            throw new InvalidOperationException("No se puede editar una relación deshabilitada. Reactívela primero.");
        }

        var cod = NormalizarCodProveedor(dto.CodProveedor);
        await ValidarProveedorAsync(cod, ct);

        if (await _context.alm_articulo_proveedors.AsNoTracking()
                .AnyAsync(p => p.articulo_id == articuloId && p.cod_proveedor == cod && p.id != id, ct))
        {
            throw new InvalidOperationException("El artículo ya tiene ese proveedor asignado.");
        }

        if (dto.Principal)
        {
            await DesmarcarPrincipalAsync(articuloId, id, ct);
        }

        entity.cod_proveedor = cod;
        entity.codigo_upc = ClasificacionNormalizer.Opcional(dto.CodigoUpc, 40);
        // El costo ya no se maneja por proveedor (se lleva en Existencias).
        entity.principal = dto.Principal;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.id;
        dto.CodProveedor = cod;
        dto.Activo = true;
        await ResolverNombresAsync(new[] { dto }, ct);
        return dto;
    }

    public async Task<bool> DeshabilitarAsync(int articuloId, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_articulo_proveedors.FirstOrDefaultAsync(p => p.id == id && p.articulo_id == articuloId, ct);
        if (entity is null) return false;
        if (!entity.activo) return true;

        // Se limpia la marca de principal para no chocar con el índice único parcial.
        entity.activo = false;
        entity.principal = false;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReactivarAsync(int articuloId, int id, string user, CancellationToken ct = default)
    {
        if (id <= 0) throw new ArgumentOutOfRangeException(nameof(id));
        var entity = await _context.alm_articulo_proveedors.FirstOrDefaultAsync(p => p.id == id && p.articulo_id == articuloId, ct);
        if (entity is null) return false;
        if (entity.activo) return true;

        await ValidarProveedorAsync(entity.cod_proveedor, ct);

        entity.activo = true;
        entity.usuariomodificacion = ClasificacionNormalizer.Usuario(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Resuelve el nombre del proveedor (prv_proveedores es keyless/multiempresa; se filtra por company manualmente).</summary>
    private async Task ResolverNombresAsync(IReadOnlyCollection<ArticuloProveedorDto> filas, CancellationToken ct)
    {
        if (filas.Count == 0) return;

        var codes = filas.Select(f => f.CodProveedor).Distinct().ToList();
        var companyId = _company.GetCompanyId();

        var nombres = await _context.prv_proveedores.AsNoTracking()
            .Where(p => p.company_id == companyId && codes.Contains(p.cod_proveedor))
            .Select(p => new { p.cod_proveedor, p.nombre })
            .ToListAsync(ct);

        var map = nombres
            .GroupBy(x => x.cod_proveedor)
            .ToDictionary(g => g.Key, g => g.First().nombre);

        foreach (var f in filas)
        {
            if (map.TryGetValue(f.CodProveedor, out var nombre))
            {
                f.ProveedorNombre = nombre;
            }
        }
    }

    private async Task ValidarArticuloAsync(int articuloId, CancellationToken ct)
    {
        if (!await _context.alm_articulos.AsNoTracking().AnyAsync(a => a.id == articuloId, ct))
        {
            throw new KeyNotFoundException("El artículo no existe.");
        }
    }

    private async Task ValidarProveedorAsync(string cod, CancellationToken ct)
    {
        var companyId = _company.GetCompanyId();
        var existe = await _context.prv_proveedores.AsNoTracking()
            .AnyAsync(p => p.company_id == companyId && p.cod_proveedor == cod && (p.status == null || p.status == true), ct);

        if (!existe)
        {
            throw new InvalidOperationException("El proveedor seleccionado no existe o está inactivo.");
        }
    }

    private async Task DesmarcarPrincipalAsync(int articuloId, int? exceptId, CancellationToken ct)
    {
        var principales = await _context.alm_articulo_proveedors
            .Where(p => p.articulo_id == articuloId && p.principal && (exceptId == null || p.id != exceptId.Value))
            .ToListAsync(ct);

        if (principales.Count == 0) return;

        foreach (var p in principales)
        {
            p.principal = false;
        }
        await _context.SaveChangesAsync(ct);
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
}
