using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Contabilidad;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Contabilidad;

public class TerceroService : ITerceroService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public TerceroService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<TerceroDto>> GetTercerosAsync(CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        return await _context.con_terceros
            .AsNoTracking()
            .Where(t => t.company_id == companyId)
            .OrderBy(t => t.code)
            .Select(t => new TerceroDto(
                t.third_party_id,
                t.code,
                t.name,
                t.description,
                t.tax_id,
                t.category,
                t.is_supplier,
                t.is_customer,
                t.status,
                t.created_at,
                t.created_by))
            .ToListAsync(ct);
    }

    public async Task<long> SaveTerceroAsync(TerceroUpsertDto request, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        if (request.ThirdPartyId.HasValue)
        {
            var existing = await _context.con_terceros
                .Where(t => t.third_party_id == request.ThirdPartyId && t.company_id == companyId)
                .FirstOrDefaultAsync(ct)
                ?? throw new InvalidOperationException($"Tercero {request.ThirdPartyId} no encontrado");

            existing.code = request.Code;
            existing.name = request.Name;
            existing.description = request.Description;
            existing.tax_id = request.TaxId;
            existing.category = request.Category;
            existing.is_supplier = request.IsSupplier;
            existing.is_customer = request.IsCustomer;
            existing.status = request.Status;
            existing.updated_at = DateTime.UtcNow;
            existing.updated_by = request.User;

            await _context.SaveChangesAsync(ct);
            return existing.third_party_id;
        }

        // Validar código único
        var duplicado = await _context.con_terceros
            .AnyAsync(t => t.company_id == companyId && t.code == request.Code, ct);
        if (duplicado)
            throw new InvalidOperationException($"Ya existe un tercero con código '{request.Code}' en esta empresa");

        var entity = new con_tercero
        {
            company_id = companyId,
            code = request.Code,
            name = request.Name,
            description = request.Description,
            tax_id = request.TaxId,
            category = request.Category,
            is_supplier = request.IsSupplier,
            is_customer = request.IsCustomer,
            status = request.Status,
            created_at = DateTime.UtcNow,
            created_by = request.User
        };

        _context.con_terceros.Add(entity);
        await _context.SaveChangesAsync(ct);
        return entity.third_party_id;
    }

    public async Task<bool> DeleteTerceroAsync(long thirdPartyId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();

        var entity = await _context.con_terceros
            .Where(t => t.third_party_id == thirdPartyId && t.company_id == companyId)
            .FirstOrDefaultAsync(ct);

        if (entity == null)
            return false;

        // Verificar que no esté siendo usado en partidas
        var enUso = await _context.con_partida_dtls
            .AnyAsync(d => d.third_party_id == thirdPartyId && d.company_id == companyId, ct);

        if (enUso)
            throw new InvalidOperationException("No se puede eliminar: este tercero está siendo usado en partidas contables");

        _context.con_terceros.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<SincronizarTercerosResultDto> SincronizarDesdeProveedoresYClientesAsync(
        string userId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        var now = DateTime.UtcNow;
        var mensajes = new List<string>();
        int provSincronizados = 0, cliSincronizados = 0;
        int provOmitidos = 0, cliOmitidos = 0;
        int errores = 0;

        // Obtener terceros existentes para esta empresa (índice por code)
        var tercerosExistentes = await _context.con_terceros
            .Where(t => t.company_id == companyId)
            .ToDictionaryAsync(t => t.code, ct);

        // ── Sincronizar proveedores ──
        // Proyectar solo columnas necesarias para evitar columnas faltantes en BD
        var proveedores = await _context.prv_proveedores
            .AsNoTracking()
            .Select(p => new { p.cod_proveedor, p.nombre, p.rtn, p.status })
            .ToListAsync(ct);

        foreach (var prov in proveedores)
        {
            var code = $"PRV-{prov.cod_proveedor}";

            if (tercerosExistentes.ContainsKey(code))
            {
                // Actualizar nombre y RTN si cambió
                var existing = tercerosExistentes[code];
                var cambio = false;

                if (existing.name != prov.nombre)
                { existing.name = prov.nombre; cambio = true; }

                if (existing.tax_id != prov.rtn)
                { existing.tax_id = prov.rtn; cambio = true; }

                if (!existing.is_supplier)
                { existing.is_supplier = true; cambio = true; }

                if (cambio)
                {
                    existing.updated_at = now;
                    existing.updated_by = userId;
                    provSincronizados++;
                }
                else
                {
                    provOmitidos++;
                }
                continue;
            }

            var entity = new con_tercero
            {
                company_id = companyId,
                code = code,
                name = prov.nombre,
                description = $"Proveedor: {prov.nombre}",
                tax_id = prov.rtn,
                category = "SUPPLIER",
                is_supplier = true,
                is_customer = false,
                status = prov.status == true ? "ACTIVE" : "INACTIVE",
                created_at = now,
                created_by = userId
            };

            _context.con_terceros.Add(entity);
            tercerosExistentes[code] = entity;
            provSincronizados++;
        }

        // ── Sincronizar clientes ──
        var clientes = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.company_id == companyId)
            .ToListAsync(ct);

        foreach (var cli in clientes)
        {
            var code = $"CLI-{cli.maestro_cliente_clave}";

            if (tercerosExistentes.ContainsKey(code))
            {
                var existing = tercerosExistentes[code];
                var cambio = false;

                if (existing.name != cli.maestro_cliente_nombre)
                { existing.name = cli.maestro_cliente_nombre; cambio = true; }

                if (existing.tax_id != cli.maestro_cliente_rtn)
                { existing.tax_id = cli.maestro_cliente_rtn; cambio = true; }

                if (!existing.is_customer)
                { existing.is_customer = true; cambio = true; }

                if (cambio)
                {
                    existing.updated_at = now;
                    existing.updated_by = userId;
                    cliSincronizados++;
                }
                else
                {
                    cliOmitidos++;
                }
                continue;
            }

            var entity = new con_tercero
            {
                company_id = companyId,
                code = code,
                name = cli.maestro_cliente_nombre,
                description = $"Cliente: {cli.maestro_cliente_nombre}",
                tax_id = cli.maestro_cliente_rtn,
                category = "CUSTOMER",
                is_supplier = false,
                is_customer = true,
                status = cli.estado ? "ACTIVE" : "INACTIVE",
                created_at = now,
                created_by = userId
            };

            _context.con_terceros.Add(entity);
            tercerosExistentes[code] = entity;
            cliSincronizados++;
        }

        await _context.SaveChangesAsync(ct);

        mensajes.Add($"Proveedores: {provSincronizados} sincronizados, {provOmitidos} sin cambios");
        mensajes.Add($"Clientes: {cliSincronizados} sincronizados, {cliOmitidos} sin cambios");
        mensajes.Add($"Total terceros: {tercerosExistentes.Count}");

        return new SincronizarTercerosResultDto(
            provSincronizados, cliSincronizados,
            provOmitidos, cliOmitidos,
            errores, mensajes);
    }
}
