using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.Medidores;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Medidores;

public class MedidoresService : IMedidoresService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public MedidoresService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<MedidorListDto>> SearchAsync(MedidorFilterDto filtro, CancellationToken ct = default)
    {
        var query = BuildQuery(filtro);

        return await query
            .Select(m => new
            {
                Medidor = m,
                Cliente = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente)
                    .FirstOrDefault()
            })
            .OrderBy(x => x.Medidor.maestro_medidor_numero)
            .Select(x => new MedidorListDto(
                x.Medidor.maestro_medidor_id,
                x.Medidor.maestro_medidor_numero,
                x.Medidor.maestro_medidor_marca,
                x.Medidor.maestro_medidor_diametro,
                x.Medidor.maestro_medidor_fecha_instala,
                x.Medidor.maestro_medidor_acueducto,
                x.Medidor.estado,
                x.Cliente != null ? x.Cliente.maestro_cliente_clave : null,
                x.Cliente != null ? x.Cliente.maestro_cliente_nombre : null,
                x.Medidor.medidor_clase_codigo))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<MedidorListItemDto>> GetPagedAsync(
        MedidorFilterDto? filtro,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        if (take <= 0)
        {
            take = 50;
        }

        if (take > 500)
        {
            take = 500;
        }

        if (skip < 0)
        {
            skip = 0;
        }

        var query = BuildQuery(filtro);
        query = ApplySort(query, sortField, sortDesc);

        var total = await query.CountAsync(ct);
        var items = await query
            .Select(m => new
            {
                Medidor = m,
                Cliente = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente)
                    .FirstOrDefault()
            })
            .Skip(skip)
            .Take(take)
            .Select(x => new MedidorListItemDto
            {
                Id = x.Medidor.maestro_medidor_id,
                Numero = x.Medidor.maestro_medidor_numero,
                Marca = x.Medidor.maestro_medidor_marca,
                FechaInstalacion = x.Medidor.maestro_medidor_fecha_instala,
                Diametro = x.Medidor.maestro_medidor_diametro,
                Empleado = x.Medidor.maestro_medidor_empleado,
                Acueducto = x.Medidor.maestro_medidor_acueducto,
                Activo = x.Medidor.estado,
                ClienteClave = x.Cliente != null ? x.Cliente.maestro_cliente_clave : null,
                ClienteNombre = x.Cliente != null ? x.Cliente.maestro_cliente_nombre : null
            })
            .ToListAsync(ct);

        return new PagedResult<MedidorListItemDto>(items, total);
    }

    public async Task<MedidorEditDto?> GetEditByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.maestro_medidors
            .AsNoTracking()
            .Where(m => m.maestro_medidor_id == id)
            .Select(m => new MedidorEditDto
            {
                Id = m.maestro_medidor_id,
                Numero = m.maestro_medidor_numero,
                Marca = m.maestro_medidor_marca,
                FechaInstalacion = m.maestro_medidor_fecha_instala,
                Diametro = m.maestro_medidor_diametro,
                Empleado = m.maestro_medidor_empleado,
                Acueducto = m.maestro_medidor_acueducto,
                Activo = m.estado,
                ClaseMedidorCodigo = m.medidor_clase_codigo
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MedidorEditDto> CreateAsync(MedidorEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var numero = NormalizeRequired(dto.Numero, 50, "numero", uppercase: true);
        var marca = NormalizeOptional(dto.Marca, 50, "marca");
        var empleado = NormalizeOptional(dto.Empleado, 50, "empleado");
        var acueducto = NormalizeOptional(dto.Acueducto, 20, "acueducto");
        var fechaInstalacion = NormalizeDate(dto.FechaInstalacion);
        var diametro = NormalizeDiametro(dto.Diametro);

        var exists = await _context.maestro_medidors
            .AsNoTracking()
            .AnyAsync(m => m.maestro_medidor_numero == numero, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un medidor con el numero {numero}.");
        }

        var now = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var entity = new maestro_medidor
        {
            company_id = _currentCompanyService.GetCompanyId(),
            maestro_medidor_numero = numero,
            maestro_medidor_marca = marca,
            maestro_medidor_fecha_instala = fechaInstalacion,
            maestro_medidor_diametro = diametro,
            maestro_medidor_empleado = empleado,
            maestro_medidor_acueducto = acueducto,
            estado = dto.Activo,
            medidor_clase_codigo = string.IsNullOrWhiteSpace(dto.ClaseMedidorCodigo)
                ? null
                : dto.ClaseMedidorCodigo.Trim().ToUpperInvariant(),
            usuariocreacion = NormalizeUser(user),
            fechacreacion = now
        };

        _context.maestro_medidors.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.maestro_medidor_id;
        dto.Numero = numero;
        dto.Marca = marca;
        dto.FechaInstalacion = fechaInstalacion;
        dto.Diametro = diametro;
        dto.Empleado = empleado;
        dto.Acueducto = acueducto;
        dto.ClaseMedidorCodigo = entity.medidor_clase_codigo;
        return dto;
    }

    public async Task<MedidorEditDto> UpdateAsync(int id, MedidorEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El medidor no es valido.");
        }

        var entity = await _context.maestro_medidors.FirstOrDefaultAsync(m => m.maestro_medidor_id == id, ct);
        if (entity is null)
        {
            throw new KeyNotFoundException("El medidor no existe.");
        }

        var numero = NormalizeRequired(dto.Numero, 50, "numero", uppercase: true);
        var marca = NormalizeOptional(dto.Marca, 50, "marca");
        var empleado = NormalizeOptional(dto.Empleado, 50, "empleado");
        var acueducto = NormalizeOptional(dto.Acueducto, 20, "acueducto");
        var fechaInstalacion = NormalizeDate(dto.FechaInstalacion);
        var diametro = NormalizeDiametro(dto.Diametro);

        var exists = await _context.maestro_medidors
            .AsNoTracking()
            .AnyAsync(m => m.maestro_medidor_numero == numero && m.maestro_medidor_id != id, ct);

        if (exists)
        {
            throw new InvalidOperationException($"Ya existe un medidor con el numero {numero}.");
        }

        entity.maestro_medidor_numero = numero;
        entity.maestro_medidor_marca = marca;
        entity.maestro_medidor_fecha_instala = fechaInstalacion;
        entity.maestro_medidor_diametro = diametro;
        entity.maestro_medidor_empleado = empleado;
        entity.maestro_medidor_acueducto = acueducto;
        entity.medidor_clase_codigo = string.IsNullOrWhiteSpace(dto.ClaseMedidorCodigo)
            ? null
            : dto.ClaseMedidorCodigo.Trim().ToUpperInvariant();
        entity.estado = dto.Activo;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.maestro_medidor_id;
        dto.Numero = numero;
        dto.Marca = marca;
        dto.FechaInstalacion = fechaInstalacion;
        dto.Diametro = diametro;
        dto.Empleado = empleado;
        dto.Acueducto = acueducto;
        dto.ClaseMedidorCodigo = entity.medidor_clase_codigo;
        return dto;
    }

    public async Task<bool> DeactivateAsync(int id, string user, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(id), "El medidor no es valido.");
        }

        var entity = await _context.maestro_medidors.FirstOrDefaultAsync(m => m.maestro_medidor_id == id, ct);
        if (entity is null)
        {
            return false;
        }

        if (!entity.estado)
        {
            return true;
        }

        entity.estado = false;
        entity.usuariomodificacion = NormalizeUser(user);
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<MedidorDetailDto?> GetAsync(int id, CancellationToken ct = default)
    {
        var medidor = await _context.maestro_medidors
            .AsNoTracking()
            .Where(m => m.maestro_medidor_id == id)
            .Select(m => new
            {
                Medidor = m,
                Cliente = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);

        if (medidor is null)
            return null;

        var cliente = medidor.Cliente;
        var historico = await GetHistorialAsync(id, 12, ct);

        // [Sprint1/FaseC 2026-05-05] configuracion_app_lectura_medidores dropeada;
        // lista vacia. La configuracion del app vive ahora en adm_* o snapshot V3.
        var configuraciones = new List<string>();

        return new MedidorDetailDto(
            medidor.Medidor.maestro_medidor_id,
            medidor.Medidor.maestro_medidor_numero,
            medidor.Medidor.maestro_medidor_marca,
            medidor.Medidor.maestro_medidor_diametro,
            medidor.Medidor.maestro_medidor_fecha_instala,
            medidor.Medidor.maestro_medidor_acueducto,
            medidor.Medidor.estado,
            cliente?.maestro_cliente_clave,
            cliente?.maestro_cliente_nombre,
            cliente?.barrio_codigo,
            historico,
            configuraciones);
    }

    public async Task<IReadOnlyList<MedidorHistorialDto>> GetHistorialAsync(int medidorId, int take = 12, CancellationToken ct = default)
    {
        var medidor = await _context.maestro_medidors
            .AsNoTracking()
            .Where(m => m.maestro_medidor_id == medidorId)
            .Select(m => new
            {
                Numero = m.maestro_medidor_numero,
                Claves = m.cliente_detalles
                    .Select(cd => cd.maestro_cliente != null ? cd.maestro_cliente.maestro_cliente_clave : null)
            })
            .FirstOrDefaultAsync(ct);

        if (medidor is null)
            return Array.Empty<MedidorHistorialDto>();

        var claves = medidor.Claves
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct()
            .ToArray();

        var query = _context.historicomedicions.AsNoTracking()
            .Where(h => h.contador == medidor.Numero ||
                        (claves.Length > 0 && h.clave != null && claves.Contains(h.clave)));

        return await query
            .OrderByDescending(h => h.fecha)
            .ThenByDescending(h => h.ide)
            .Take(take)
            .Select(h => new MedidorHistorialDto(
                h.fecha,
                h.lect_act,
                h.lect_ant,
                h.consumo,
                h.condicion,
                h.observacion,
                h.usuario))
            .ToListAsync(ct);
    }

    public async Task<bool> AssignToClienteAsync(int medidorId, int clienteId, CancellationToken ct = default)
    {
        var medidor = await _context.maestro_medidors
            .Include(m => m.cliente_detalles)
            .FirstOrDefaultAsync(m => m.maestro_medidor_id == medidorId, ct);

        if (medidor is null)
            return false;

        var cliente = await _context.cliente_maestros
            .Include(c => c.cliente_detalles)
            .FirstOrDefaultAsync(c => c.maestro_cliente_id == clienteId, ct);

        if (cliente is null)
            return false;

        var detalle = cliente.cliente_detalles
            .OrderBy(cd => cd.detalle_cliente_id)
            .FirstOrDefault();

        if (detalle is null)
        {
            detalle = new cliente_detalle
            {
                maestro_cliente_id = clienteId,
                detalle_cliente_direccion = cliente.cliente_detalles.FirstOrDefault()?.detalle_cliente_direccion,
                detalle_cliente_telefono = cliente.cliente_detalles.FirstOrDefault()?.detalle_cliente_telefono,
                estado = true
            };
            cliente.cliente_detalles.Add(detalle);
        }

        detalle.maestro_medidor_id = medidorId;
        cliente.maestro_cliente_tiene_medidor = true;
        medidor.estado = true;
        medidor.usuariomodificacion = "api";
        // PostgreSQL timestamp without time zone expects Kind=Unspecified/Local.
        medidor.fechamodificacion = DateTime.Now;

        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task RegistrarLecturaSinMedidorAsync(string clave, DateTime fecha, decimal lectura, string usuario, CancellationToken ct = default)
    {
        var historico = new historicosinmedidor
        {
            cuenta = clave,
            fecha = fecha,
            usuario = usuario,
            ano = fecha.Year,
            mes = fecha.Month,
            numerofactura = null,
            correlativocai = null,
            idcai = null
        };

        await _context.historicosinmedidors.AddAsync(historico, ct);
        await _context.SaveChangesAsync(ct);
    }

    private IQueryable<maestro_medidor> BuildQuery(MedidorFilterDto? filtro)
    {
        filtro ??= new MedidorFilterDto(null, null, null, null, null);

        var query = _context.maestro_medidors.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Numero))
            query = query.Where(m => EF.Functions.ILike(m.maestro_medidor_numero, $"%{filtro.Numero}%"));

        if (!string.IsNullOrWhiteSpace(filtro.Marca))
            query = query.Where(m => EF.Functions.ILike(m.maestro_medidor_marca ?? string.Empty, $"%{filtro.Marca}%"));

        if (filtro.Estado.HasValue)
            query = query.Where(m => m.estado == filtro.Estado.Value);

        if (filtro.Asignado.HasValue)
        {
            query = filtro.Asignado.Value
                ? query.Where(m => m.cliente_detalles.Any())
                : query.Where(m => !m.cliente_detalles.Any());
        }

        if (!string.IsNullOrWhiteSpace(filtro.ClienteClave))
        {
            query = query.Where(m => m.cliente_detalles.Any(cd =>
                cd.maestro_cliente != null &&
                EF.Functions.ILike(cd.maestro_cliente.maestro_cliente_clave ?? string.Empty, $"%{filtro.ClienteClave}%")));
        }

        return query;
    }

    private static IQueryable<maestro_medidor> ApplySort(IQueryable<maestro_medidor> query, string? sortField, bool sortDesc)
    {
        var field = sortField?.Trim();
        if (string.IsNullOrWhiteSpace(field))
        {
            return query.OrderBy(m => m.maestro_medidor_numero);
        }

        return field.ToLowerInvariant() switch
        {
            "numero" => sortDesc ? query.OrderByDescending(m => m.maestro_medidor_numero) : query.OrderBy(m => m.maestro_medidor_numero),
            "marca" => sortDesc ? query.OrderByDescending(m => m.maestro_medidor_marca) : query.OrderBy(m => m.maestro_medidor_marca),
            "fechainstalacion" => sortDesc ? query.OrderByDescending(m => m.maestro_medidor_fecha_instala) : query.OrderBy(m => m.maestro_medidor_fecha_instala),
            "diametro" => sortDesc ? query.OrderByDescending(m => m.maestro_medidor_diametro) : query.OrderBy(m => m.maestro_medidor_diametro),
            "empleado" => sortDesc ? query.OrderByDescending(m => m.maestro_medidor_empleado) : query.OrderBy(m => m.maestro_medidor_empleado),
            "acueducto" => sortDesc ? query.OrderByDescending(m => m.maestro_medidor_acueducto) : query.OrderBy(m => m.maestro_medidor_acueducto),
            "activo" => sortDesc ? query.OrderByDescending(m => m.estado) : query.OrderBy(m => m.estado),
            _ => query.OrderBy(m => m.maestro_medidor_numero)
        };
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"El {fieldName} es obligatorio.", nameof(value));
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} no puede superar {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName, bool uppercase = false)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El {fieldName} no puede superar {maxLength} caracteres.", nameof(value));
        }

        return uppercase ? normalized.ToUpperInvariant() : normalized;
    }

    private static decimal? NormalizeDiametro(decimal? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        var normalized = Math.Round(value.Value, 2);
        if (normalized < 0 || normalized > 99.99m)
        {
            throw new ArgumentException("El diametro debe estar entre 0 y 99.99.", nameof(value));
        }

        return normalized;
    }

    private static DateTime? NormalizeDate(DateTime? value)
    {
        if (!value.HasValue)
        {
            return null;
        }

        return DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
    }

    private static string NormalizeUser(string? user)
    {
        return string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
    }
}
