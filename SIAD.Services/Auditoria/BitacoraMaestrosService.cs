using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Auditoria;
using SIAD.Data;

namespace SIAD.Services.Auditoria;

public sealed class BitacoraMaestrosService : IBitacoraMaestrosService
{
    private readonly SiadDbContext _context;
    public BitacoraMaestrosService(SiadDbContext context) => _context = context;

    public async Task<IReadOnlyList<BitacoraMaestroListItemDto>> BuscarAsync(BitacoraMaestroFilterDto filtro, CancellationToken ct = default)
    {
        var q = _context.bitacora_maestros.AsNoTracking().AsQueryable();
        if (filtro.Desde is { } d) q = q.Where(b => b.fecha >= d.Date);
        if (filtro.Hasta is { } h) q = q.Where(b => b.fecha < h.Date.AddDays(1));
        if (!string.IsNullOrWhiteSpace(filtro.Modulo)) q = q.Where(b => b.modulo == filtro.Modulo);
        if (!string.IsNullOrWhiteSpace(filtro.Tabla)) q = q.Where(b => b.tabla == filtro.Tabla);
        if (!string.IsNullOrWhiteSpace(filtro.Accion)) q = q.Where(b => b.accion == filtro.Accion);
        if (!string.IsNullOrWhiteSpace(filtro.Usuario))
        {
            var like = $"%{filtro.Usuario.Trim()}%";
            q = q.Where(b => EF.Functions.ILike(b.usuario, like));
        }

        return await q.OrderByDescending(b => b.fecha).Take(BitacoraMaestrosConstantes.MaxFilas)
            .Select(b => new BitacoraMaestroListItemDto
            {
                Id = b.bitacora_maestro_id, Fecha = b.fecha, Usuario = b.usuario,
                Modulo = b.modulo, Tabla = b.tabla, Entidad = b.entidad,
                Accion = b.accion, Descripcion = b.descripcion, RegistroId = b.registro_id,
                ValoresAnteriores = b.valores_anteriores, ValoresNuevos = b.valores_nuevos
            }).ToListAsync(ct);
    }
}
