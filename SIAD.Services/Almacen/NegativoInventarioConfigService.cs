using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Interruptor de existencia negativa en salidas, por empresa (cfg_inventario_negativo). El tenant
/// lo resuelve el filtro global del contexto: hay a lo sumo una fila visible (la de la empresa
/// actual), y el stamping de <c>SaveChanges</c> pone el <c>company_id</c> al insertar. Mismo patrón
/// que <see cref="IsvCompraConfigService"/>.
/// </summary>
public sealed class NegativoInventarioConfigService : INegativoInventarioConfigService
{
    private readonly SiadDbContext _context;

    public NegativoInventarioConfigService(SiadDbContext context) => _context = context;

    public async Task<NegativoInventarioConfigDto> ObtenerAsync(CancellationToken ct = default)
    {
        var cfg = await _context.cfg_inventario_negativos.AsNoTracking().FirstOrDefaultAsync(ct);
        return new NegativoInventarioConfigDto { Permitir = cfg?.permitir ?? false };
    }

    public async Task<NegativoInventarioConfigDto> GuardarAsync(NegativoInventarioConfigDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        if (usuario.Length > 100) usuario = usuario[..100];

        var cfg = await _context.cfg_inventario_negativos.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            // company_id lo estampa el override de SaveChanges (ICompanyScopedEntity).
            cfg = new cfg_inventario_negativo
            {
                permitir = dto.Permitir,
                usuariocreacion = usuario,
                fechacreacion = ahora
            };
            _context.cfg_inventario_negativos.Add(cfg);
        }
        else
        {
            cfg.permitir = dto.Permitir;
            cfg.usuariomodificacion = usuario;
            cfg.fechamodificacion = ahora;
        }

        await _context.SaveChangesAsync(ct);
        return new NegativoInventarioConfigDto { Permitir = cfg.permitir };
    }
}
