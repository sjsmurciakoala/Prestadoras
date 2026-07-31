using System.Globalization;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.CaptacionPagos;
using SIAD.Data;

namespace SIAD.Services.Cobros;

/// <summary>
/// Catálogos de apoyo de la caja única: lookup de clientes y cuentas
/// bancarias. F7 H5: son los dos únicos sobrevivientes del módulo
/// CaptacionPagos (todo lo demás eran fachadas legacy sin callers) y se
/// mudan al módulo de cobros, donde viven sus consumidores.
/// </summary>
public interface ICatalogosCobroService
{
    Task<IReadOnlyList<ClienteComboDto>> ListarClientesAsync(string? query = null, int? maxResults = null, CancellationToken ct = default);
    Task<IReadOnlyList<BancoDto>> ListarBancosAsync(CancellationToken ct = default);
}

public class CatalogosCobroService : ICatalogosCobroService
{
    private readonly SiadDbContext _context;

    public CatalogosCobroService(SiadDbContext context) => _context = context;

    public async Task<IReadOnlyList<ClienteComboDto>> ListarClientesAsync(string? query = null, int? maxResults = null, CancellationToken ct = default)
    {
        var clientes = _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.estado)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query))
        {
            var term = $"%{query.Trim()}%";
            clientes = clientes.Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, term)
                || EF.Functions.ILike(c.maestro_cliente_nombre, term));
        }

        var take = maxResults ?? (string.IsNullOrWhiteSpace(query) ? 10 : 100);
        return await clientes
            .OrderBy(c => c.maestro_cliente_nombre)
            .Take(take)
            .Select(c => new ClienteComboDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault()
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BancoDto>> ListarBancosAsync(CancellationToken ct = default)
    {
        var cuentasBancarias = await _context.ban_cuenta
            .AsNoTracking()
            .Where(c => c.activo)
            .Select(c => new
            {
                c.banco_cuenta_id,
                c.ban_banco_id,
                c.code,
                c.nombre,
                c.banco_nombre,
                c.numero_cuenta,
                BancoCode = c.ban_banco != null ? c.ban_banco.code : null,
                BancoNombre = c.ban_banco != null ? c.ban_banco.nombre : null
            })
            .ToListAsync(ct);

        if (cuentasBancarias.Count > 0)
        {
            return cuentasBancarias
                .Select(c =>
                {
                    var codigo = !string.IsNullOrWhiteSpace(c.BancoCode)
                        ? c.BancoCode.Trim()
                        : !string.IsNullOrWhiteSpace(c.code)
                            ? c.code.Trim()
                            : c.banco_cuenta_id.ToString(CultureInfo.InvariantCulture);

                    var nombreBanco = !string.IsNullOrWhiteSpace(c.banco_nombre)
                        ? c.banco_nombre.Trim()
                        : !string.IsNullOrWhiteSpace(c.BancoNombre)
                            ? c.BancoNombre.Trim()
                            : !string.IsNullOrWhiteSpace(c.nombre)
                                ? c.nombre.Trim()
                                : codigo;

                    var nombre = string.IsNullOrWhiteSpace(c.numero_cuenta)
                        ? $"{codigo} - {nombreBanco}"
                        : $"{codigo} - {nombreBanco} ({c.numero_cuenta.Trim()})";

                    return new BancoDto
                    {
                        BancoCuentaId = c.banco_cuenta_id,
                        BancoId = c.ban_banco_id,
                        Codigo = codigo,
                        Nombre = nombre
                    };
                })
                .OrderBy(b => b.Nombre)
                .ToList();
        }

        // Fallback legacy mientras no haya catálogo bancario configurado.
        return await _context.recolectoras
            .AsNoTracking()
            .OrderBy(b => b.descripcion)
            .Select(b => new BancoDto
            {
                Codigo = b.codigo,
                Nombre = string.IsNullOrWhiteSpace(b.descripcion) ? b.codigo : $"{b.codigo} - {b.descripcion}"
            })
            .ToListAsync(ct);
    }
}
