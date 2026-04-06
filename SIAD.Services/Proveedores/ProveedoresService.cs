using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Entities;
using SIAD.Data;
using System.Globalization;

namespace SIAD.Services.Proveedores;

public class ProveedoresService : IProveedoresService
{
    private const int ProveedorCodigoLongitud = 6;
    private const int ProveedorCodigoBase = 1000;
    private const int ProveedorCodigoMaximo = 999999;
    private readonly SiadDbContext _context;

    public ProveedoresService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ProveedorListItemDto>> GetProveedoresAsync(CancellationToken cancellationToken = default)
    {
        return await SearchProveedoresAsync(new ProveedorFilterDto(), cancellationToken);
    }

    public async Task<IReadOnlyList<ProveedorListItemDto>> SearchProveedoresAsync(ProveedorFilterDto filtro, CancellationToken cancellationToken = default)
    {
        filtro ??= new ProveedorFilterDto();

        var proveedores = _context.prv_proveedores.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            var patron = $"%{filtro.Codigo.Trim()}%";
            proveedores = proveedores.Where(p => EF.Functions.ILike(p.cod_proveedor, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Nombre))
        {
            var patron = $"%{filtro.Nombre.Trim()}%";
            proveedores = proveedores.Where(p => EF.Functions.ILike(p.nombre, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Rtn))
        {
            var patron = $"%{filtro.Rtn.Trim()}%";
            proveedores = proveedores.Where(p => p.rtn != null && EF.Functions.ILike(p.rtn, patron));
        }

        if (filtro.SoloActivos)
        {
            proveedores = proveedores.Where(p => p.status == true);
        }

        return await proveedores
            .OrderBy(p => p.cod_proveedor)
            .Select(p => new ProveedorListItemDto(
                p.cod_proveedor ?? string.Empty,
                p.nombre ?? string.Empty,
                p.direccion,
                p.rtn,
                p.telefono,
                p.status == true))
            .ToListAsync(cancellationToken);
    }

    public async Task<ProveedorDetailDto?> GetProveedorAsync(string codigo, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(codigo))
        {
            return null;
        }

        var codigoNormalizado = codigo.Trim();

        var proveedor = await _context.prv_proveedores
            .AsNoTracking()
            .Where(p => p.cod_proveedor == codigoNormalizado)
            .Select(p => new
            {
                p.cod_proveedor,
                p.nombre,
                p.razon_social,
                p.rtn,
                p.direccion,
                p.nombre_contacto,
                p.telefono,
                p.fax,
                p.email,
                p.pagina_web,
                p.cuenta_contable,
                p.cuenta_bancaria,
                p.nombrebanco1,
                p.nombrebanco2,
                p.cod_tipoproveedor,
                p.compras_acum,
                p.compras_dolares,
                p.saldo_anterior,
                p.saldo_ant_doleres,
                p.saldo_actual,
                p.saldo_act_dolares,
                p.fecha_creacion,
                p.fecha_modificacion,
                p.status
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (proveedor is null)
        {
            return null;
        }

        var tipoProveedor = await _context.prv_tipoproveedors
            .AsNoTracking()
            .Where(t => t.cod_tipoproveedor == proveedor.cod_tipoproveedor)
            .Select(t => t.nombre)
            .FirstOrDefaultAsync(cancellationToken);

        return new ProveedorDetailDto(
            proveedor.cod_proveedor ?? codigoNormalizado,
            proveedor.nombre ?? string.Empty,
            proveedor.razon_social,
            proveedor.rtn,
            proveedor.direccion,
            proveedor.nombre_contacto,
            proveedor.telefono,
            proveedor.fax,
            proveedor.email,
            proveedor.pagina_web,
            proveedor.cuenta_contable,
            proveedor.cuenta_bancaria,
            proveedor.nombrebanco1,
            proveedor.nombrebanco2,
            proveedor.cod_tipoproveedor,
            tipoProveedor,
            proveedor.compras_acum,
            proveedor.compras_dolares,
            proveedor.saldo_anterior,
            proveedor.saldo_ant_doleres,
            proveedor.saldo_actual,
            proveedor.saldo_act_dolares,
            proveedor.fecha_creacion,
            proveedor.fecha_modificacion,
            proveedor.status == true);
    }

    public async Task<IReadOnlyList<ProveedorTipoLookupDto>> GetTiposAsync(CancellationToken cancellationToken = default)
    {
        return await _context.prv_tipoproveedors
            .AsNoTracking()
            .OrderBy(t => t.nombre)
            .Select(t => new ProveedorTipoLookupDto(
                t.cod_tipoproveedor,
                t.nombre ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<string> CreateAsync(ProveedorUpsertDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = await GenerateProveedorCodeAsync(cancellationToken);
        var nombre = NormalizeRequired(dto.Nombre);
        var direccion = NormalizeRequired(dto.Direccion);
        var cuentaContable = NormalizeRequired(dto.CuentaContable);
        var cuentaBancaria = NormalizeRequired(dto.CuentaBancaria);

        if (dto.CodTipoProveedor <= 0)
        {
            throw new ArgumentException("El tipo de proveedor es requerido.", nameof(dto.CodTipoProveedor));
        }

        var now = DateTime.UtcNow;

        await _context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO public.prv_proveedores
              (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
               fecha_creacion, fecha_modificacion, status, cuenta_bancaria,
               compras_acum, compras_dolares, saldo_actual, saldo_act_dolares,
               saldo_anterior, saldo_ant_doleres, razon_social, rtn, nombre_contacto,
               telefono, email, nombrebanco1, nombrebanco2)
              VALUES
              ({0}, {1}, {2}, {3}, {4},
               {5}, {6}, {7}, {8},
               {9}, {10}, {11}, {12},
               {13}, {14}, {15}, {16}, {17},
               {18}, {19}, {20}, {21})",
            new object[]
            {
                codigo,
                (short)dto.CodTipoProveedor,
                nombre,
                cuentaContable,
                direccion,
                now,
                now,
                dto.Activo,
                cuentaBancaria,
                0d,
                0d,
                0d,
                0d,
                0d,
                0d,
                NormalizeOptional(dto.RazonSocial),
                NormalizeOptional(dto.Rtn),
                NormalizeOptional(dto.NombreContacto),
                NormalizeOptional(dto.Telefono),
                NormalizeOptional(dto.Email),
                NormalizeOptional(dto.Banco1),
                NormalizeOptional(dto.Banco2)
            });

        return codigo;
    }

    public async Task UpdateAsync(string codigo, ProveedorUpsertDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigoNormalizado = NormalizeRequired(codigo);
        var nombre = NormalizeRequired(dto.Nombre);
        var direccion = NormalizeRequired(dto.Direccion);
        var cuentaContable = NormalizeRequired(dto.CuentaContable);
        var cuentaBancaria = NormalizeRequired(dto.CuentaBancaria);

        if (dto.CodTipoProveedor <= 0)
        {
            throw new ArgumentException("El tipo de proveedor es requerido.", nameof(dto.CodTipoProveedor));
        }

        var now = DateTime.UtcNow;

        var rows = await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE public.prv_proveedores
              SET cod_tipoproveedor = {1},
                  nombre = {2},
                  cuenta_contable = {3},
                  direccion = {4},
                  fecha_modificacion = {5},
                  status = {6},
                  cuenta_bancaria = {7},
                  razon_social = {8},
                  rtn = {9},
                  nombre_contacto = {10},
                  telefono = {11},
                  email = {12},
                  nombrebanco1 = {13},
                  nombrebanco2 = {14}
              WHERE cod_proveedor = {0}",
            new object[]
            {
                codigoNormalizado,
                (short)dto.CodTipoProveedor,
                nombre,
                cuentaContable,
                direccion,
                now,
                dto.Activo,
                cuentaBancaria,
                NormalizeOptional(dto.RazonSocial),
                NormalizeOptional(dto.Rtn),
                NormalizeOptional(dto.NombreContacto),
                NormalizeOptional(dto.Telefono),
                NormalizeOptional(dto.Email),
                NormalizeOptional(dto.Banco1),
                NormalizeOptional(dto.Banco2)
            });

        if (rows == 0)
        {
            throw new KeyNotFoundException($"No se encontro el proveedor {codigoNormalizado}.");
        }
    }

    public async Task DeleteAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var codigoNormalizado = NormalizeRequired(codigo);

        var rows = await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM public.prv_proveedores WHERE cod_proveedor = {0}",
            new object[] { codigoNormalizado });

        if (rows == 0)
        {
            throw new KeyNotFoundException($"No se encontro el proveedor {codigoNormalizado}.");
        }
    }

    public async Task<IReadOnlyList<TipoProveedorListItemDto>> GetTiposCatalogoAsync(CancellationToken cancellationToken = default)
    {
        return await _context.prv_tipoproveedors
            .AsNoTracking()
            .OrderBy(t => t.nombre)
            .Select(t => new TipoProveedorListItemDto(
                t.cod_tipoproveedor,
                t.nombre,
                t.observaciones))
            .ToListAsync(cancellationToken);
    }

    public async Task<TipoProveedorDetailDto?> GetTipoAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        return await _context.prv_tipoproveedors
            .AsNoTracking()
            .Where(t => t.cod_tipoproveedor == id)
            .Select(t => new TipoProveedorDetailDto(
                t.cod_tipoproveedor,
                t.nombre,
                t.observaciones))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<int> CreateTipoAsync(TipoProveedorUpsertDto dto, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var entity = new prv_tipoproveedor
        {
            nombre = NormalizeRequired(dto.Nombre),
            observaciones = NormalizeOptional(dto.Observaciones)
        };

        _context.prv_tipoproveedors.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.cod_tipoproveedor;
    }

    public async Task UpdateTipoAsync(int id, TipoProveedorUpsertDto dto, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El identificador del tipo no es valido.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(dto);

        var entity = await _context.prv_tipoproveedors
            .FirstOrDefaultAsync(t => t.cod_tipoproveedor == id, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro el tipo de proveedor {id}.");
        }

        entity.nombre = NormalizeRequired(dto.Nombre);
        entity.observaciones = NormalizeOptional(dto.Observaciones);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTipoAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El identificador del tipo no es valido.", nameof(id));
        }

        var enUso = await _context.prv_proveedores
            .AsNoTracking()
            .AnyAsync(p => p.cod_tipoproveedor == id, cancellationToken);

        if (enUso)
        {
            throw new InvalidOperationException("No se puede eliminar el tipo porque esta asignado a uno o mas proveedores.");
        }

        var entity = await _context.prv_tipoproveedors
            .FirstOrDefaultAsync(t => t.cod_tipoproveedor == id, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro el tipo de proveedor {id}.");
        }

        _context.prv_tipoproveedors.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El valor no puede estar vacio.");
        }

        return value.Trim();
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private async Task<string> GenerateProveedorCodeAsync(CancellationToken cancellationToken)
    {
        var codigos = await _context.prv_proveedores
            .AsNoTracking()
            .Select(p => p.cod_proveedor)
            .ToListAsync(cancellationToken);

        var maximoNumerico = ProveedorCodigoBase;

        foreach (var codigo in codigos)
        {
            if (string.IsNullOrWhiteSpace(codigo))
            {
                continue;
            }

            var normalizado = codigo.Trim();
            if (!int.TryParse(normalizado, NumberStyles.None, CultureInfo.InvariantCulture, out var valor))
            {
                continue;
            }

            if (valor <= ProveedorCodigoMaximo && valor > maximoNumerico)
            {
                maximoNumerico = valor;
            }
        }

        var siguiente = maximoNumerico + 1;
        if (siguiente > ProveedorCodigoMaximo)
        {
            throw new InvalidOperationException("No fue posible generar el codigo del proveedor.");
        }

        return siguiente.ToString($"D{ProveedorCodigoLongitud}", CultureInfo.InvariantCulture);
    }
}
