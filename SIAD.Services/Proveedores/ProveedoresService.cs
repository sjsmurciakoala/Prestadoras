using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;
using SIAD.Core.Constants;
using SIAD.Core.DTOs.Proveedores;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;
using System.Data;
using System.Globalization;

namespace SIAD.Services.Proveedores;

public class ProveedoresService : IProveedoresService
{
    private const string ProveedorTableName = "prv_proveedores";
    private const string ProveedorBancoTableName = "prv_bancos";
    private const string ProveedorCuentaBancariaTableName = "prv_proveedor_cuenta_bancaria";
    private const int ProveedorCodigoLongitud = 6;
    private const int ProveedorCodigoBase = 1000;
    private const int ProveedorCodigoMaximo = 999999;
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;
    private readonly Dictionary<string, bool> _columnExists = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeKind> _timestampKinds = new(StringComparer.OrdinalIgnoreCase);

    public ProveedoresService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task EnsureProveedorGenericoAsync(CancellationToken cancellationToken = default)
    {
        var companyId = EnsureCompanyId();
        await NormalizeProveedorGenericoLegacyCodeAsync(companyId, cancellationToken);

        var codigoProveedor = ProveedoresConstants.CodigoProveedorGenericoCompromisos;

        var exists = await _context.prv_proveedores
            .AsNoTracking()
            .AnyAsync(
                p => p.company_id == companyId && p.cod_proveedor == codigoProveedor,
                cancellationToken);

        if (exists)
        {
            return;
        }

        var tipoProveedorId = await ResolveTipoProveedorGenericoAsync(cancellationToken);
        var fechaCreacion = await GetCurrentDatabaseTimestampAsync(ProveedorTableName, "fecha_creacion", cancellationToken);
        var usuario = NormalizeUser("system");
        var cuentaContable = await ResolveCuentaContableProveedorGenericoAsync(companyId, cancellationToken);

        if (await HasLegacyProveedorBankColumnsAsync(cancellationToken))
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"INSERT INTO public.prv_proveedores
                  (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                   fecha_creacion, fecha_modificacion, usuario_creo, usuario_modifica, status, cuenta_bancaria,
                   compras_acum, compras_dolares, saldo_actual, saldo_act_dolares,
                   saldo_anterior, saldo_ant_doleres, razon_social, rtn, nombre_contacto,
                   telefono, email, nombrebanco1, nombrebanco2, company_id)
                  SELECT
                   {codigoProveedor}, {(short)tipoProveedorId}, {ProveedoresConstants.NombreProveedorGenericoCompromisos}, {cuentaContable}, {ProveedoresConstants.DireccionProveedorGenericoCompromisos},
                   {fechaCreacion}, NULL, {usuario}, NULL, TRUE, NULL,
                   NULL, NULL, NULL, NULL,
                   NULL, NULL, NULL, NULL, NULL,
                   NULL, NULL, NULL, NULL, {companyId}
                  WHERE NOT EXISTS (
                    SELECT 1
                    FROM public.prv_proveedores
                    WHERE cod_proveedor = {codigoProveedor}
                      AND company_id = {companyId})",
                cancellationToken);

            return;
        }

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT INTO public.prv_proveedores
              (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
               fecha_creacion, fecha_modificacion, usuario_creo, usuario_modifica, status,
               compras_acum, compras_dolares, saldo_actual, saldo_act_dolares,
               saldo_anterior, saldo_ant_doleres, razon_social, rtn, nombre_contacto,
               telefono, email, company_id)
              SELECT
               {codigoProveedor}, {(short)tipoProveedorId}, {ProveedoresConstants.NombreProveedorGenericoCompromisos}, {cuentaContable}, {ProveedoresConstants.DireccionProveedorGenericoCompromisos},
               {fechaCreacion}, NULL, {usuario}, NULL, TRUE,
               NULL, NULL, NULL, NULL,
               NULL, NULL, NULL, NULL, NULL,
               NULL, NULL, {companyId}
              WHERE NOT EXISTS (
                SELECT 1
                FROM public.prv_proveedores
                WHERE cod_proveedor = {codigoProveedor}
                  AND company_id = {companyId})",
            cancellationToken);
    }

    public async Task<IReadOnlyList<ProveedorListItemDto>> GetProveedoresAsync(CancellationToken cancellationToken = default)
    {
        return await SearchProveedoresAsync(new ProveedorFilterDto(), cancellationToken);
    }

    public async Task<IReadOnlyList<ProveedorListItemDto>> SearchProveedoresAsync(ProveedorFilterDto filtro, CancellationToken cancellationToken = default)
    {
        filtro ??= new ProveedorFilterDto();

        await EnsureProveedorGenericoAsync(cancellationToken);

        var companyId = EnsureCompanyId();
        var proveedores = _context.prv_proveedores
            .AsNoTracking()
            .Where(p => p.company_id == companyId)
            .AsQueryable();

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

        if (IsProveedorGenerico(codigoNormalizado))
        {
            await EnsureProveedorGenericoAsync(cancellationToken);
            codigoNormalizado = ProveedoresConstants.CodigoProveedorGenericoCompromisos;
        }

        var companyId = EnsureCompanyId();
        var proveedor = await _context.prv_proveedores
            .AsNoTracking()
            .Where(p => p.cod_proveedor == codigoNormalizado && p.company_id == companyId)
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

        var cuentasBancarias = await LoadCuentasBancariasAsync(codigoNormalizado, cancellationToken);

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
            cuentasBancarias,
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

    public async Task<IReadOnlyList<ProveedorBancoLookupDto>> GetBancosAsync(CancellationToken cancellationToken = default)
    {
        var bancos = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

        if (await TableExistsAsync(ProveedorBancoTableName, cancellationToken))
        {
            AddNormalizedValues(
                bancos,
                await _context.prv_bancos
                    .AsNoTracking()
                    .Where(x => x.activo)
                    .OrderBy(x => x.nombre)
                    .Select(x => x.nombre)
                    .ToListAsync(cancellationToken));
        }

        if (await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            AddNormalizedValues(
                bancos,
                await _context.prv_proveedor_cuentas_bancarias
                    .AsNoTracking()
                    .OrderBy(x => x.banco)
                    .Select(x => x.banco)
                    .ToListAsync(cancellationToken));
        }

        return bancos
            .Select(x => new ProveedorBancoLookupDto { Nombre = x })
            .ToList();
    }

    public async Task<IReadOnlyList<ProveedorBancoListItemDto>> GetBancosCatalogoAsync(CancellationToken cancellationToken = default)
    {
        await EnsureProveedorBancoTableExistsAsync(cancellationToken);

        var bancos = await _context.prv_bancos
            .AsNoTracking()
            .OrderBy(x => x.nombre)
            .ToListAsync(cancellationToken);

        var usageCounts = await BuildBancoUsageCountsAsync(cancellationToken);

        return bancos
            .Select(x => new ProveedorBancoListItemDto(
                x.prv_banco_id,
                x.nombre.Trim(),
                x.activo,
                usageCounts.TryGetValue(x.nombre.Trim(), out var count) ? count : 0))
            .ToList();
    }

    public async Task<ProveedorBancoDetailDto?> GetBancoAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            return null;
        }

        await EnsureProveedorBancoTableExistsAsync(cancellationToken);

        var banco = await _context.prv_bancos
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.prv_banco_id == id, cancellationToken);

        if (banco is null)
        {
            return null;
        }

        var usageCounts = await BuildBancoUsageCountsAsync(cancellationToken);
        var nombre = banco.nombre.Trim();

        return new ProveedorBancoDetailDto(
            banco.prv_banco_id,
            nombre,
            banco.activo,
            usageCounts.TryGetValue(nombre, out var count) ? count : 0);
    }

    public async Task<long> CreateBancoAsync(
        ProveedorBancoUpsertDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        await EnsureProveedorBancoTableExistsAsync(cancellationToken);

        var nombre = NormalizeRequired(dto.Nombre, 80, "nombre del banco");
        var usuario = NormalizeUser(user);
        await ValidateUniqueBancoNameAsync(nombre, null, cancellationToken);

        var now = await GetCurrentDatabaseTimestampAsync(ProveedorBancoTableName, "fecha_creacion", cancellationToken);
        var entity = new prv_banco
        {
            nombre = nombre,
            activo = dto.Activo,
            fecha_creacion = now,
            usuario_creo = usuario
        };

        _context.prv_bancos.Add(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return entity.prv_banco_id;
    }

    public async Task UpdateBancoAsync(
        long id,
        ProveedorBancoUpsertDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El identificador del banco no es valido.", nameof(id));
        }

        ArgumentNullException.ThrowIfNull(dto);
        await EnsureProveedorBancoTableExistsAsync(cancellationToken);

        var entity = await _context.prv_bancos
            .FirstOrDefaultAsync(x => x.prv_banco_id == id, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro el banco de proveedor {id}.");
        }

        var now = await GetCurrentDatabaseTimestampAsync(ProveedorBancoTableName, "fecha_modificacion", cancellationToken);
        var nombreNuevo = NormalizeRequired(dto.Nombre, 80, "nombre del banco");
        var nombreAnterior = entity.nombre.Trim();
        var usuario = NormalizeUser(user);

        await ValidateUniqueBancoNameAsync(nombreNuevo, id, cancellationToken);

        entity.nombre = nombreNuevo;
        entity.activo = dto.Activo;
        entity.fecha_modificacion = now;
        entity.usuario_modifica = usuario;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        if (!string.Equals(nombreAnterior, nombreNuevo, StringComparison.OrdinalIgnoreCase))
        {
            await RenameBancoEnProveedoresAsync(nombreAnterior, nombreNuevo, usuario, cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    public async Task DeleteBancoAsync(long id, CancellationToken cancellationToken = default)
    {
        if (id <= 0)
        {
            throw new ArgumentException("El identificador del banco no es valido.", nameof(id));
        }

        await EnsureProveedorBancoTableExistsAsync(cancellationToken);

        var entity = await _context.prv_bancos
            .FirstOrDefaultAsync(x => x.prv_banco_id == id, cancellationToken);

        if (entity is null)
        {
            throw new KeyNotFoundException($"No se encontro el banco de proveedor {id}.");
        }

        var usageCounts = await BuildBancoUsageCountsAsync(cancellationToken);
        var nombre = entity.nombre.Trim();
        if (usageCounts.TryGetValue(nombre, out var count) && count > 0)
        {
            throw new InvalidOperationException("No se puede eliminar el banco porque esta asignado a uno o mas proveedores.");
        }

        _context.prv_bancos.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> CreateAsync(
        ProveedorUpsertDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = await GenerateProveedorCodeAsync(cancellationToken);
        var nombre = NormalizeRequired(dto.Nombre);
        var direccion = NormalizeRequired(dto.Direccion);
        var cuentaContable = NormalizeRequired(dto.CuentaContable);
        var cuentasBancarias = PrepareCuentasBancarias(dto);

        if (dto.CodTipoProveedor <= 0)
        {
            throw new ArgumentException("El tipo de proveedor es requerido.", nameof(dto.CodTipoProveedor));
        }

        var fechaCreacion = await GetCurrentDatabaseTimestampAsync(ProveedorTableName, "fecha_creacion", cancellationToken);
        var fechaModificacion = await GetCurrentDatabaseTimestampAsync(ProveedorTableName, "fecha_modificacion", cancellationToken);
        var usuario = NormalizeUser(user);
        string? usuarioModifica = null;
        var companyId = (int)_currentCompanyService.GetCompanyId();
        var hasLegacyBankColumns = await HasLegacyProveedorBankColumnsAsync(cancellationToken);
        var legacyFields = hasLegacyBankColumns ? BuildLegacyCompatibilityFields(cuentasBancarias) : null;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (hasLegacyBankColumns && legacyFields is not null)
        {
            await _context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO public.prv_proveedores
                  (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                   fecha_creacion, fecha_modificacion, usuario_creo, usuario_modifica, status, cuenta_bancaria,
                   compras_acum, compras_dolares, saldo_actual, saldo_act_dolares,
                   saldo_anterior, saldo_ant_doleres, razon_social, rtn, nombre_contacto,
                   telefono, email, nombrebanco1, nombrebanco2, company_id)
                  VALUES
                  ({0}, {1}, {2}, {3}, {4},
                   {5}, {6}, {7}, {8}, {9}, {10},
                   {11}, {12}, {13}, {14},
                   {15}, {16}, {17}, {18}, {19},
                   {20}, {21}, {22}, {23}, {24})",
                new object[]
                {
                    codigo,
                    (short)dto.CodTipoProveedor,
                    nombre,
                    cuentaContable,
                    direccion,
                    fechaCreacion,
                    fechaModificacion,
                    usuario,
                    OptionalText(usuarioModifica),
                    dto.Activo,
                    OptionalText(legacyFields.CuentaBancaria),
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    OptionalText(NormalizeOptional(dto.RazonSocial)),
                    OptionalText(NormalizeOptional(dto.Rtn)),
                    OptionalText(NormalizeOptional(dto.NombreContacto)),
                    OptionalText(NormalizeOptional(dto.Telefono)),
                    OptionalText(NormalizeOptional(dto.Email)),
                    OptionalText(legacyFields.Banco1),
                    OptionalText(legacyFields.Banco2),
                    companyId
                });
        }
        else
        {
            await _context.Database.ExecuteSqlRawAsync(
                @"INSERT INTO public.prv_proveedores
                  (cod_proveedor, cod_tipoproveedor, nombre, cuenta_contable, direccion,
                   fecha_creacion, fecha_modificacion, usuario_creo, usuario_modifica, status,
                   compras_acum, compras_dolares, saldo_actual, saldo_act_dolares,
                   saldo_anterior, saldo_ant_doleres, razon_social, rtn, nombre_contacto,
                   telefono, email, company_id)
                  VALUES
                  ({0}, {1}, {2}, {3}, {4},
                   {5}, {6}, {7}, {8}, {9},
                   {10}, {11}, {12}, {13},
                   {14}, {15}, {16}, {17}, {18},
                   {19}, {20}, {21})",
                new object[]
                {
                    codigo,
                    (short)dto.CodTipoProveedor,
                    nombre,
                    cuentaContable,
                    direccion,
                    fechaCreacion,
                    fechaModificacion,
                    usuario,
                    OptionalText(usuarioModifica),
                    dto.Activo,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    0d,
                    OptionalText(NormalizeOptional(dto.RazonSocial)),
                    OptionalText(NormalizeOptional(dto.Rtn)),
                    OptionalText(NormalizeOptional(dto.NombreContacto)),
                    OptionalText(NormalizeOptional(dto.Telefono)),
                    OptionalText(NormalizeOptional(dto.Email)),
                    companyId
                });
        }

        await SyncBancosCatalogoAsync(cuentasBancarias, user, cancellationToken);
        await SyncCuentasBancariasAsync(codigo, cuentasBancarias, user, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return codigo;
    }

    public async Task UpdateAsync(
        string codigo,
        ProveedorUpsertDto dto,
        string user,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigoNormalizado = NormalizeRequired(codigo);
        var nombre = NormalizeRequired(dto.Nombre);
        var direccion = NormalizeRequired(dto.Direccion);
        var cuentaContable = NormalizeRequired(dto.CuentaContable);
        var cuentasBancarias = PrepareCuentasBancarias(dto);

        if (dto.CodTipoProveedor <= 0)
        {
            throw new ArgumentException("El tipo de proveedor es requerido.", nameof(dto.CodTipoProveedor));
        }

        var now = await GetCurrentDatabaseTimestampAsync(ProveedorTableName, "fecha_modificacion", cancellationToken);
        var usuario = NormalizeUser(user);
        var hasLegacyBankColumns = await HasLegacyProveedorBankColumnsAsync(cancellationToken);
        var legacyFields = hasLegacyBankColumns ? BuildLegacyCompatibilityFields(cuentasBancarias) : null;

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        int rows;
        if (hasLegacyBankColumns && legacyFields is not null)
        {
            rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE public.prv_proveedores
                  SET cod_tipoproveedor = {1},
                      nombre = {2},
                      cuenta_contable = {3},
                      direccion = {4},
                      fecha_modificacion = {5},
                      usuario_modifica = {6},
                      status = {7},
                      cuenta_bancaria = {8},
                      razon_social = {9},
                      rtn = {10},
                      nombre_contacto = {11},
                      telefono = {12},
                      email = {13},
                      nombrebanco1 = {14},
                      nombrebanco2 = {15}
                  WHERE cod_proveedor = {0}",
                new object[]
                {
                    codigoNormalizado,
                    (short)dto.CodTipoProveedor,
                    nombre,
                    cuentaContable,
                    direccion,
                    now,
                    usuario,
                    dto.Activo,
                    OptionalText(legacyFields.CuentaBancaria),
                    OptionalText(NormalizeOptional(dto.RazonSocial)),
                    OptionalText(NormalizeOptional(dto.Rtn)),
                    OptionalText(NormalizeOptional(dto.NombreContacto)),
                    OptionalText(NormalizeOptional(dto.Telefono)),
                    OptionalText(NormalizeOptional(dto.Email)),
                    OptionalText(legacyFields.Banco1),
                    OptionalText(legacyFields.Banco2)
                });
        }
        else
        {
            rows = await _context.Database.ExecuteSqlRawAsync(
                @"UPDATE public.prv_proveedores
                  SET cod_tipoproveedor = {1},
                      nombre = {2},
                      cuenta_contable = {3},
                      direccion = {4},
                      fecha_modificacion = {5},
                      usuario_modifica = {6},
                      status = {7},
                      razon_social = {8},
                      rtn = {9},
                      nombre_contacto = {10},
                      telefono = {11},
                      email = {12}
                  WHERE cod_proveedor = {0}",
                new object[]
                {
                    codigoNormalizado,
                    (short)dto.CodTipoProveedor,
                    nombre,
                    cuentaContable,
                    direccion,
                    now,
                    usuario,
                    dto.Activo,
                    OptionalText(NormalizeOptional(dto.RazonSocial)),
                    OptionalText(NormalizeOptional(dto.Rtn)),
                    OptionalText(NormalizeOptional(dto.NombreContacto)),
                    OptionalText(NormalizeOptional(dto.Telefono)),
                    OptionalText(NormalizeOptional(dto.Email))
                });
        }

        if (rows == 0)
        {
            throw new KeyNotFoundException($"No se encontro el proveedor {codigoNormalizado}.");
        }

        await SyncBancosCatalogoAsync(cuentasBancarias, user, cancellationToken);
        await SyncCuentasBancariasAsync(codigoNormalizado, cuentasBancarias, user, cancellationToken);
        await tx.CommitAsync(cancellationToken);
    }

    public async Task DeleteAsync(string codigo, CancellationToken cancellationToken = default)
    {
        var codigoNormalizado = NormalizeRequired(codigo);

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        if (await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            await _context.prv_proveedor_cuentas_bancarias
                .Where(x => x.cod_proveedor == codigoNormalizado)
                .ExecuteDeleteAsync(cancellationToken);
        }

        var rows = await _context.Database.ExecuteSqlRawAsync(
            "DELETE FROM public.prv_proveedores WHERE cod_proveedor = {0}",
            new object[] { codigoNormalizado });

        if (rows == 0)
        {
            throw new KeyNotFoundException($"No se encontro el proveedor {codigoNormalizado}.");
        }

        await tx.CommitAsync(cancellationToken);
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

    private async Task<List<ProveedorCuentaBancariaDto>> LoadCuentasBancariasAsync(
        string codigoProveedor,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            return new List<ProveedorCuentaBancariaDto>();
        }

        return await _context.prv_proveedor_cuentas_bancarias
            .AsNoTracking()
            .Where(x => x.cod_proveedor == codigoProveedor)
            .OrderBy(x => x.orden)
            .ThenBy(x => x.proveedor_cuenta_bancaria_id)
            .Select(x => new ProveedorCuentaBancariaDto
            {
                ProveedorCuentaBancariaId = x.proveedor_cuenta_bancaria_id,
                Banco = x.banco,
                CuentaBancaria = x.cuenta_bancaria,
                Orden = x.orden
            })
            .ToListAsync(cancellationToken);
    }

    private async Task<Dictionary<string, int>> BuildBancoUsageCountsAsync(CancellationToken cancellationToken)
    {
        var usageMap = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

        if (await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            var cuentas = await _context.prv_proveedor_cuentas_bancarias
                .AsNoTracking()
                .Select(x => new { x.cod_proveedor, x.banco })
                .ToListAsync(cancellationToken);

            foreach (var item in cuentas)
            {
                RegisterBancoUsage(usageMap, item.banco, item.cod_proveedor);
            }
        }

        return usageMap.ToDictionary(x => x.Key, x => x.Value.Count, StringComparer.OrdinalIgnoreCase);
    }

    private static void RegisterBancoUsage(
        Dictionary<string, HashSet<string>> usageMap,
        string? banco,
        string? codigoProveedor)
    {
        var bancoNormalizado = NormalizeOptional(banco, 80, "banco");
        var proveedorNormalizado = NormalizeOptional(codigoProveedor, 20, "codigo de proveedor");

        if (bancoNormalizado is null || proveedorNormalizado is null)
        {
            return;
        }

        if (!usageMap.TryGetValue(bancoNormalizado, out var proveedores))
        {
            proveedores = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            usageMap[bancoNormalizado] = proveedores;
        }

        proveedores.Add(proveedorNormalizado);
    }

    private async Task<DateTime> GetCurrentDatabaseTimestampAsync(
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var kind = await GetTimestampKindAsync(tableName, columnName, cancellationToken);
        var now = DateTime.UtcNow;

        return kind == DateTimeKind.Utc
            ? now
            : DateTime.SpecifyKind(now, DateTimeKind.Unspecified);
    }

    private async Task<DateTimeKind> GetTimestampKindAsync(
        string tableName,
        string columnName,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"{tableName}:{columnName}";
        if (_timestampKinds.TryGetValue(cacheKey, out var cachedKind))
        {
            return cachedKind;
        }

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @table_name
  AND column_name = @column_name
LIMIT 1;";
            command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);
            command.Parameters.AddWithValue("column_name", NpgsqlDbType.Varchar, columnName);

            var dataType = await command.ExecuteScalarAsync(cancellationToken) as string;
            var kind = string.Equals(dataType, "timestamp with time zone", StringComparison.OrdinalIgnoreCase)
                ? DateTimeKind.Utc
                : DateTimeKind.Unspecified;

            _timestampKinds[cacheKey] = kind;
            return kind;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task EnsureProveedorBancoTableExistsAsync(CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorBancoTableName, cancellationToken))
        {
            throw new InvalidOperationException("No existe la tabla prv_bancos. Aplique primero el script de base de datos correspondiente.");
        }
    }

    private async Task ValidateUniqueBancoNameAsync(
        string nombre,
        long? excludeId,
        CancellationToken cancellationToken)
    {
        var existingNames = await _context.prv_bancos
            .AsNoTracking()
            .Where(x => !excludeId.HasValue || x.prv_banco_id != excludeId.Value)
            .Select(x => new { x.prv_banco_id, x.nombre })
            .ToListAsync(cancellationToken);

        var exists = existingNames.Any(x =>
            !string.IsNullOrWhiteSpace(x.nombre) &&
            string.Equals(x.nombre.Trim(), nombre, StringComparison.OrdinalIgnoreCase));

        if (exists)
        {
            throw new InvalidOperationException("Ya existe un banco de proveedor con ese nombre.");
        }
    }

    private async Task RenameBancoEnProveedoresAsync(
        string nombreAnterior,
        string nombreNuevo,
        string user,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            return;
        }

        var detalleNow = await GetCurrentDatabaseTimestampAsync(
            ProveedorCuentaBancariaTableName,
            "fecha_modificacion",
            cancellationToken);
        await _context.Database.ExecuteSqlRawAsync(
            @"UPDATE public.prv_proveedor_cuenta_bancaria
              SET banco = {1},
                  fecha_modificacion = {2},
                  usuario_modifica = {3}
              WHERE lower(btrim(banco)) = lower(btrim({0}))",
            new object[] { nombreAnterior, nombreNuevo, detalleNow, NormalizeUser(user) },
            cancellationToken);
    }

    private async Task SyncBancosCatalogoAsync(
        IReadOnlyList<ProveedorCuentaBancariaDto> cuentasBancarias,
        string user,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorBancoTableName, cancellationToken))
        {
            return;
        }

        var usuario = NormalizeUser(user);
        var fechaCreacion = await GetCurrentDatabaseTimestampAsync(
            ProveedorBancoTableName,
            "fecha_creacion",
            cancellationToken);
        var fechaModificacion = await GetCurrentDatabaseTimestampAsync(
            ProveedorBancoTableName,
            "fecha_modificacion",
            cancellationToken);

        var bancosNormalizados = cuentasBancarias
            .Select(x => NormalizeOptional(x.Banco, 80, "banco"))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (bancosNormalizados.Count == 0)
        {
            return;
        }

        var existentes = await _context.prv_bancos.ToListAsync(cancellationToken);
        var existentesMap = existentes
            .Where(x => !string.IsNullOrWhiteSpace(x.nombre))
            .GroupBy(x => x.nombre.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToDictionary(x => x.nombre.Trim(), StringComparer.OrdinalIgnoreCase);

        var hasChanges = false;

        foreach (var nombre in bancosNormalizados)
        {
            if (nombre is null)
            {
                continue;
            }

            if (existentesMap.TryGetValue(nombre, out var existente))
            {
                if (!existente.activo)
                {
                    existente.activo = true;
                    existente.fecha_modificacion = fechaModificacion;
                    existente.usuario_modifica = usuario;
                    hasChanges = true;
                }

                continue;
            }

            var nuevoBanco = new prv_banco
            {
                nombre = nombre,
                activo = true,
                fecha_creacion = fechaCreacion,
                usuario_creo = usuario
            };
            _context.prv_bancos.Add(nuevoBanco);
            existentesMap[nombre] = nuevoBanco;
            hasChanges = true;
        }

        if (hasChanges)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task SyncCuentasBancariasAsync(
        string codigoProveedor,
        IReadOnlyList<ProveedorCuentaBancariaDto> cuentasBancarias,
        string user,
        CancellationToken cancellationToken)
    {
        if (!await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            return;
        }

        var usuario = NormalizeUser(user);
        var fechaCreacion = await GetCurrentDatabaseTimestampAsync(
            ProveedorCuentaBancariaTableName,
            "fecha_creacion",
            cancellationToken);
        var fechaModificacion = await GetCurrentDatabaseTimestampAsync(
            ProveedorCuentaBancariaTableName,
            "fecha_modificacion",
            cancellationToken);

        var existentes = await _context.prv_proveedor_cuentas_bancarias
            .Where(x => x.cod_proveedor == codigoProveedor)
            .ToListAsync(cancellationToken);

        var existentesPorId = existentes.ToDictionary(x => x.proveedor_cuenta_bancaria_id);
        var idsEnviados = cuentasBancarias
            .Where(x => x.ProveedorCuentaBancariaId.HasValue && x.ProveedorCuentaBancariaId.Value > 0)
            .Select(x => x.ProveedorCuentaBancariaId!.Value)
            .ToHashSet();

        foreach (var existente in existentes.Where(x => !idsEnviados.Contains(x.proveedor_cuenta_bancaria_id)))
        {
            _context.prv_proveedor_cuentas_bancarias.Remove(existente);
        }

        foreach (var item in cuentasBancarias)
        {
            var orden = item.Orden > 0 ? item.Orden : 1;
            if (item.ProveedorCuentaBancariaId is long id && id > 0)
            {
                if (!existentesPorId.TryGetValue(id, out var existente))
                {
                    throw new InvalidOperationException(
                        $"No se encontro la cuenta bancaria {id} para el proveedor {codigoProveedor}.");
                }

                var hayCambios = !string.Equals(existente.banco, item.Banco, StringComparison.Ordinal)
                    || !string.Equals(existente.cuenta_bancaria, item.CuentaBancaria, StringComparison.Ordinal)
                    || existente.orden != orden;

                existente.banco = item.Banco!;
                existente.cuenta_bancaria = item.CuentaBancaria!;
                existente.orden = orden;

                if (hayCambios)
                {
                    existente.fecha_modificacion = fechaModificacion;
                    existente.usuario_modifica = usuario;
                }

                continue;
            }

            _context.prv_proveedor_cuentas_bancarias.Add(new prv_proveedor_cuenta_bancaria
            {
                cod_proveedor = codigoProveedor,
                banco = item.Banco!,
                cuenta_bancaria = item.CuentaBancaria!,
                orden = orden,
                fecha_creacion = fechaCreacion,
                usuario_creo = usuario
            });
        }

        await _context.SaveChangesAsync(cancellationToken);
    }

    private static List<ProveedorCuentaBancariaDto> PrepareCuentasBancarias(ProveedorUpsertDto dto)
    {
        var cuentas = new List<ProveedorCuentaBancariaDto>();
        var source = dto.CuentasBancarias ?? new List<ProveedorCuentaBancariaDto>();

        for (var i = 0; i < source.Count; i++)
        {
            var banco = NormalizeOptional(source[i].Banco, 80, $"banco en la fila {i + 1}");
            var cuentaBancaria = NormalizeOptional(source[i].CuentaBancaria, 50, $"cuenta bancaria en la fila {i + 1}");

            if (banco is null && cuentaBancaria is null)
            {
                continue;
            }

            if (banco is null || cuentaBancaria is null)
            {
                throw new ArgumentException($"Debe completar banco y cuenta bancaria en la fila {i + 1}.", nameof(dto.CuentasBancarias));
            }

            cuentas.Add(new ProveedorCuentaBancariaDto
            {
                ProveedorCuentaBancariaId = source[i].ProveedorCuentaBancariaId,
                Banco = banco,
                CuentaBancaria = cuentaBancaria,
                Orden = cuentas.Count + 1
            });
        }

        if (cuentas.Any(x => string.IsNullOrWhiteSpace(x.CuentaBancaria)))
        {
            throw new ArgumentException("Cada detalle bancario debe tener cuenta bancaria.", nameof(dto.CuentasBancarias));
        }

        if (cuentas.Any(x => string.IsNullOrWhiteSpace(x.Banco)))
        {
            throw new ArgumentException("Cada detalle bancario debe tener banco.", nameof(dto.CuentasBancarias));
        }

        var duplicado = cuentas
            .GroupBy(
                x => $"{x.Banco!.Trim().ToUpperInvariant()}|{x.CuentaBancaria!.Trim().ToUpperInvariant()}",
                StringComparer.Ordinal)
            .Any(g => g.Count() > 1);

        if (duplicado)
        {
            throw new ArgumentException("No puede repetir la misma cuenta bancaria para el mismo banco.", nameof(dto.CuentasBancarias));
        }

        return cuentas;
    }

    private static void AddNormalizedValues(SortedSet<string> target, IEnumerable<string?> values)
    {
        foreach (var value in values)
        {
            AddNormalizedValue(target, value);
        }
    }

    private static void AddNormalizedValue(SortedSet<string> target, string? value)
    {
        var normalized = NormalizeOptional(value, 80, "banco");
        if (normalized is not null)
        {
            target.Add(normalized);
        }
    }

    private async Task<bool> TableExistsAsync(string tableName, CancellationToken cancellationToken)
    {
        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT 1
FROM information_schema.tables
WHERE table_schema = 'public'
  AND table_name = @table_name
LIMIT 1;";
            command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return result is not null && result is not DBNull;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<bool> HasLegacyProveedorBankColumnsAsync(CancellationToken cancellationToken)
    {
        return await ColumnExistsAsync(ProveedorTableName, "cuenta_bancaria", cancellationToken)
            && await ColumnExistsAsync(ProveedorTableName, "nombrebanco1", cancellationToken)
            && await ColumnExistsAsync(ProveedorTableName, "nombrebanco2", cancellationToken);
    }

    private async Task<bool> ColumnExistsAsync(string tableName, string columnName, CancellationToken cancellationToken)
    {
        var cacheKey = $"{tableName}:{columnName}";
        if (_columnExists.TryGetValue(cacheKey, out var exists))
        {
            return exists;
        }

        var connection = (NpgsqlConnection)_context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = @"
SELECT 1
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = @table_name
  AND column_name = @column_name
LIMIT 1;";
            command.Parameters.AddWithValue("table_name", NpgsqlDbType.Varchar, tableName);
            command.Parameters.AddWithValue("column_name", NpgsqlDbType.Varchar, columnName);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            exists = result is not null && result is not DBNull;
            _columnExists[cacheKey] = exists;
            return exists;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static LegacyCompatibilityFields BuildLegacyCompatibilityFields(IReadOnlyList<ProveedorCuentaBancariaDto> cuentasBancarias)
    {
        var primeraCuenta = cuentasBancarias.FirstOrDefault();

        return new LegacyCompatibilityFields(
            NormalizeOptional(primeraCuenta?.CuentaBancaria),
            NormalizeOptional(primeraCuenta?.Banco),
            NormalizeOptional(cuentasBancarias.Skip(1).FirstOrDefault()?.Banco));
    }

    private static string NormalizeRequired(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("El valor no puede estar vacio.");
        }

        return value.Trim();
    }

    private static string NormalizeRequired(string value, int maxLength, string fieldName)
    {
        var normalized = NormalizeRequired(value);
        if (normalized.Length > maxLength)
        {
            throw new ArgumentException($"El campo {fieldName} no puede superar {maxLength} caracteres.");
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    // EF Core 9's ExecuteSqlRaw infers the store type from each positional value's
    // runtime type, and DBNull has no type mapping (it throws "no store type mapping
    // for properties of type 'DBNull'"). Wrapping the optional value in a typed
    // NpgsqlParameter makes EF use it directly instead of inferring, so NULL works.
    private static NpgsqlParameter OptionalText(string? value) =>
        new() { NpgsqlDbType = NpgsqlDbType.Varchar, Value = (object?)value ?? DBNull.Value };

    private static string? NormalizeOptional(string? value, int maxLength, string fieldName)
    {
        var normalized = NormalizeOptional(value);
        if (normalized is not null && normalized.Length > maxLength)
        {
            throw new ArgumentException($"El campo {fieldName} no puede superar {maxLength} caracteres.");
        }

        return normalized;
    }

    private static string NormalizeUser(string? user)
    {
        var normalized = string.IsNullOrWhiteSpace(user) ? "system" : user.Trim();
        return normalized.Length <= 100 ? normalized : normalized[..100];
    }

    private int EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0 || companyId > int.MaxValue)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa actual.");
        }

        return (int)companyId;
    }

    private async Task NormalizeProveedorGenericoLegacyCodeAsync(int companyId, CancellationToken cancellationToken)
    {
        var codigoActual = ProveedoresConstants.CodigoProveedorGenericoCompromisos;
        var codigoLegacy = ProveedoresConstants.CodigoProveedorGenericoCompromisosLegacy;

        var actualExists = await _context.prv_proveedores
            .AsNoTracking()
            .AnyAsync(
                p => p.company_id == companyId && p.cod_proveedor == codigoActual,
                cancellationToken);

        if (actualExists)
        {
            return;
        }

        var legacyExists = await _context.prv_proveedores
            .AsNoTracking()
            .AnyAsync(
                p => p.company_id == companyId && p.cod_proveedor == codigoLegacy,
                cancellationToken);

        if (!legacyExists)
        {
            return;
        }

        await using var tx = await _context.Database.BeginTransactionAsync(cancellationToken);

        await _context.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE public.prv_proveedores
               SET cod_proveedor = {codigoActual}
               WHERE company_id = {companyId}
                 AND cod_proveedor = {codigoLegacy}
                 AND NOT EXISTS (
                    SELECT 1
                    FROM public.prv_proveedores p2
                    WHERE p2.company_id = {companyId}
                      AND p2.cod_proveedor = {codigoActual})",
            cancellationToken);

        if (await TableExistsAsync(ProveedorCuentaBancariaTableName, cancellationToken))
        {
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE public.prv_proveedor_cuenta_bancaria
                   SET cod_proveedor = {codigoActual}
                   WHERE cod_proveedor = {codigoLegacy}
                     AND NOT EXISTS (
                        SELECT 1
                        FROM public.prv_proveedor_cuenta_bancaria d2
                        WHERE d2.cod_proveedor = {codigoActual}
                          AND d2.cuenta_bancaria = public.prv_proveedor_cuenta_bancaria.cuenta_bancaria
                          AND COALESCE(d2.banco, '') = COALESCE(public.prv_proveedor_cuenta_bancaria.banco, ''))",
                cancellationToken);
        }

        await tx.CommitAsync(cancellationToken);
    }

    private async Task<int> ResolveTipoProveedorGenericoAsync(CancellationToken cancellationToken)
    {
        var tipoExistente = await _context.prv_tipoproveedors
            .AsNoTracking()
            .Where(t => t.nombre != null &&
                EF.Functions.ILike(t.nombre, ProveedoresConstants.NombreTipoProveedorGenerico))
            .OrderBy(t => t.cod_tipoproveedor)
            .Select(t => t.cod_tipoproveedor)
            .FirstOrDefaultAsync(cancellationToken);

        if (tipoExistente > 0)
        {
            return tipoExistente;
        }

        var primerTipo = await _context.prv_tipoproveedors
            .AsNoTracking()
            .OrderBy(t => t.cod_tipoproveedor)
            .Select(t => t.cod_tipoproveedor)
            .FirstOrDefaultAsync(cancellationToken);

        if (primerTipo > 0)
        {
            return primerTipo;
        }

        var nuevoTipo = new prv_tipoproveedor
        {
            nombre = ProveedoresConstants.NombreTipoProveedorGenerico,
            observaciones = "Tipo reservado para compromisos directos."
        };

        _context.prv_tipoproveedors.Add(nuevoTipo);
        await _context.SaveChangesAsync(cancellationToken);
        return nuevoTipo.cod_tipoproveedor;
    }

    private async Task<string> ResolveCuentaContableProveedorGenericoAsync(int companyId, CancellationToken cancellationToken)
    {
        var cuentasProveedor = await _context.prv_proveedores
            .AsNoTracking()
            .Where(p =>
                p.company_id == companyId &&
                p.cod_proveedor != ProveedoresConstants.CodigoProveedorGenericoCompromisos &&
                p.cod_proveedor != ProveedoresConstants.CodigoProveedorGenericoCompromisosLegacy &&
                p.status == true &&
                p.cuenta_contable != null &&
                p.cuenta_contable != string.Empty)
            .Select(p => p.cuenta_contable!)
            .Distinct()
            .ToListAsync(cancellationToken);

        return cuentasProveedor.Count == 1
            ? cuentasProveedor[0].Trim()
            : string.Empty;
    }

    private static bool IsProveedorGenerico(string? codigo)
    {
        var normalizado = codigo?.Trim();

        return string.Equals(
                   normalizado,
                   ProveedoresConstants.CodigoProveedorGenericoCompromisos,
                   StringComparison.OrdinalIgnoreCase) ||
               string.Equals(
                   normalizado,
                   ProveedoresConstants.CodigoProveedorGenericoCompromisosLegacy,
                   StringComparison.OrdinalIgnoreCase);
    }

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

    private sealed record LegacyCompatibilityFields(
        string? CuentaBancaria,
        string? Banco1,
        string? Banco2);
}
