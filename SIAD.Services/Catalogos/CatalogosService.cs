using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Catalogos;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Catalogos;

public class CatalogosService : ICatalogosService
{
    private readonly SiadDbContext _context;

    public CatalogosService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AbogadoLookupDto>> GetAbogadosAsync(CancellationToken ct = default)
    {
        return await _context.abogados
            .AsNoTracking()
            .Where(a => a.estado)
            .OrderBy(a => a.abogado_nombrecorto)
            .Select(a => new AbogadoLookupDto(
                a.abogado_id,
                a.abogado_codigo,
                !string.IsNullOrWhiteSpace(a.abogado_nombrecorto)
                    ? a.abogado_nombrecorto
                    : (a.abogado_nombrelargo ?? a.abogado_codigo)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BarrioLookupDto>> GetBarriosAsync(CancellationToken ct = default)
    {
        return await _context.barrios
            .AsNoTracking()
            .Where(b => b.estado)
            .OrderBy(b => b.descripcion)
            .Select(b => new BarrioLookupDto(
                b.barrio_codigo,
                b.descripcion))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BarrioDto>> ListarBarriosDtoAsync(CancellationToken ct = default)
    {
        return await _context.barrios
            .AsNoTracking()
            .OrderBy(b => b.barrio_codigo)
            .Select(b => new BarrioDto(b.barrio_codigo, b.descripcion, b.estado))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ServicioLookupDto>> GetServiciosAsync(CancellationToken ct = default)
    {
        return await _context.servicios
            .AsNoTracking()
            .Where(s => s.estado)
            .OrderBy(s => s.servicios_descripcioncorta)
            .Select(s => new ServicioLookupDto(
                s.servicios_id,
                s.servicios_codigo,
                s.servicios_descripcioncorta))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<TipoUsoLookupDto>> GetTiposUsoAsync(CancellationToken ct = default)
    {
        return await _context.tipo_uso_servicios
            .AsNoTracking()
            .Where(t => t.estado)
            .OrderBy(t => t.descripcion)
            .Select(t => new TipoUsoLookupDto(
                t.tipo_uso_codigo,
                t.descripcion))
            .ToListAsync(ct);
    }

    public Task<IReadOnlyList<int>> GetCategoriasPorTipoAsync(int tipoUsoCodigo, CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<int>>(Array.Empty<int>());
    }

    public async Task<BarrioDto?> GetBarrioAsync(string codigo, CancellationToken ct = default)
    {
        var b = await _context.barrios
            .AsNoTracking()
            .Where(x => x.barrio_codigo == codigo)
            .Select(x => new BarrioDto(x.barrio_codigo, x.descripcion, x.estado))
            .FirstOrDefaultAsync(ct);
        return b;
    }

    public async Task<BarrioDto> CrearBarrioAsync(BarrioCreateDto dto, string usuario, CancellationToken ct = default)
    {
        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (await _context.barrios.AnyAsync(b => b.barrio_codigo == codigo, ct))
            throw new InvalidOperationException($"Ya existe un barrio con el código {codigo}.");

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var entity = new barrio
        {
            barrio_codigo    = codigo,
            descripcion      = dto.Descripcion.Trim(),
            estado           = true,
            usuariocreacion  = usuario,
            fechacreacion    = ahora
        };
        _context.barrios.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new BarrioDto(entity.barrio_codigo, entity.descripcion, entity.estado);
    }

    public async Task<BarrioDto> ActualizarBarrioAsync(string codigo, BarrioUpdateDto dto, string usuario, CancellationToken ct = default)
    {
        var entity = await _context.barrios.FirstOrDefaultAsync(b => b.barrio_codigo == codigo, ct)
            ?? throw new KeyNotFoundException($"Barrio {codigo} no encontrado.");

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        entity.descripcion         = dto.Descripcion.Trim();
        entity.estado              = dto.Estado;
        entity.usuariomodificacion = usuario;
        entity.fechamodificacion   = ahora;

        await _context.SaveChangesAsync(ct);
        return new BarrioDto(entity.barrio_codigo, entity.descripcion, entity.estado);
    }

    public async Task EliminarBarrioAsync(string codigo, CancellationToken ct = default)
    {
        var entity = await _context.barrios.FirstOrDefaultAsync(b => b.barrio_codigo == codigo, ct)
            ?? throw new KeyNotFoundException($"Barrio {codigo} no encontrado.");

        var tieneClientes = await _context.cliente_maestros
            .AnyAsync(c => c.barrio_codigo == codigo, ct);

        if (tieneClientes)
            throw new InvalidOperationException("No se puede eliminar: el barrio tiene clientes asociados.");

        _context.barrios.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClaseMedidorLookupDto>> GetClasesMedidorAsync(CancellationToken ct = default)
    {
        return await _context.medidor_clases
            .AsNoTracking()
            .Where(c => c.estado)
            .OrderBy(c => c.medidor_clase_codigo)
            .Select(c => new ClaseMedidorLookupDto(c.medidor_clase_codigo, c.descripcion))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<ClaseMedidorDto>> ListarClasesMedidorDtoAsync(CancellationToken ct = default)
    {
        return await _context.medidor_clases
            .AsNoTracking()
            .OrderBy(c => c.medidor_clase_codigo)
            .Select(c => new ClaseMedidorDto(c.medidor_clase_codigo, c.descripcion, c.estado))
            .ToListAsync(ct);
    }

    public async Task<ClaseMedidorDto?> GetClaseMedidorAsync(string codigo, CancellationToken ct = default)
    {
        return await _context.medidor_clases
            .AsNoTracking()
            .Where(c => c.medidor_clase_codigo == codigo)
            .Select(c => new ClaseMedidorDto(c.medidor_clase_codigo, c.descripcion, c.estado))
            .FirstOrDefaultAsync(ct);
    }

    public async Task<ClaseMedidorDto> CrearClaseMedidorAsync(ClaseMedidorCreateDto dto, string usuario, CancellationToken ct = default)
    {
        var codigo = dto.Codigo.Trim().ToUpperInvariant();
        if (await _context.medidor_clases.AnyAsync(c => c.medidor_clase_codigo == codigo, ct))
            throw new InvalidOperationException($"Ya existe una clase de medidor con el código {codigo}.");

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var entity = new medidor_clase
        {
            medidor_clase_codigo = codigo,
            descripcion          = dto.Descripcion.Trim(),
            estado               = true,
            usuariocreacion      = usuario,
            fechacreacion        = ahora
        };
        _context.medidor_clases.Add(entity);
        await _context.SaveChangesAsync(ct);
        return new ClaseMedidorDto(entity.medidor_clase_codigo, entity.descripcion, entity.estado);
    }

    public async Task<ClaseMedidorDto> ActualizarClaseMedidorAsync(string codigo, ClaseMedidorUpdateDto dto, string usuario, CancellationToken ct = default)
    {
        var entity = await _context.medidor_clases.FirstOrDefaultAsync(c => c.medidor_clase_codigo == codigo, ct)
            ?? throw new KeyNotFoundException($"Clase de medidor {codigo} no encontrada.");

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        entity.descripcion         = dto.Descripcion.Trim();
        entity.estado              = dto.Estado;
        entity.usuariomodificacion = usuario;
        entity.fechamodificacion   = ahora;

        await _context.SaveChangesAsync(ct);
        return new ClaseMedidorDto(entity.medidor_clase_codigo, entity.descripcion, entity.estado);
    }

    public async Task EliminarClaseMedidorAsync(string codigo, CancellationToken ct = default)
    {
        var entity = await _context.medidor_clases.FirstOrDefaultAsync(c => c.medidor_clase_codigo == codigo, ct)
            ?? throw new KeyNotFoundException($"Clase de medidor {codigo} no encontrada.");

        // Check across all tenants — medidor_clase has no company_id
        var tieneUso = await _context.maestro_medidors
            .IgnoreQueryFilters()
            .AnyAsync(m => m.medidor_clase_codigo == codigo, ct);

        if (tieneUso)
            throw new InvalidOperationException("No se puede eliminar: la clase tiene medidores asociados.");

        _context.medidor_clases.Remove(entity);
        await _context.SaveChangesAsync(ct);
    }
}
