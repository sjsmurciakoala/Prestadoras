using Microsoft.EntityFrameworkCore;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Almacen;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Almacen;

/// <summary>
/// Configuración del ISV en compras, por empresa (cfg_compra_isv). El tenant lo resuelve el
/// filtro global del contexto: hay a lo sumo una fila visible (la de la empresa actual), y el
/// stamping de <c>SaveChanges</c> pone el <c>company_id</c> al insertar.
/// </summary>
public sealed class IsvCompraConfigService : IIsvCompraConfigService
{
    private readonly SiadDbContext _context;

    public IsvCompraConfigService(SiadDbContext context) => _context = context;

    public async Task<IsvCompraConfigDto> ObtenerAsync(CancellationToken ct = default)
    {
        var cfg = await _context.cfg_compra_isvs.AsNoTracking().FirstOrDefaultAsync(ct);
        return new IsvCompraConfigDto
        {
            Tratamiento = cfg?.tratamiento ?? TratamientoIsvCompra.Costo
        };
    }

    public async Task<IsvCompraConfigDto> GuardarAsync(IsvCompraConfigDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var tratamiento = (dto.Tratamiento ?? string.Empty).Trim().ToUpperInvariant();
        if (!TratamientoIsvCompra.EsValido(tratamiento))
        {
            throw new InvalidOperationException("El tratamiento del ISV debe ser COSTO o FISCAL.");
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var usuario = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        if (usuario.Length > 100) usuario = usuario[..100];

        var cfg = await _context.cfg_compra_isvs.FirstOrDefaultAsync(ct);
        if (cfg is null)
        {
            // company_id lo estampa el override de SaveChanges (ICompanyScopedEntity).
            cfg = new cfg_compra_isv
            {
                tratamiento = tratamiento,
                usuariocreacion = usuario,
                fechacreacion = ahora
            };
            _context.cfg_compra_isvs.Add(cfg);
        }
        else
        {
            cfg.tratamiento = tratamiento;
            cfg.usuariomodificacion = usuario;
            cfg.fechamodificacion = ahora;
        }

        await _context.SaveChangesAsync(ct);
        return new IsvCompraConfigDto { Tratamiento = cfg.tratamiento };
    }
}
