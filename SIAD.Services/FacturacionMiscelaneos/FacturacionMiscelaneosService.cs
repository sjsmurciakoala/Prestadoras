using System.Data;
using System.Text.Json;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SIAD.Core.DTOs.Common;
using SIAD.Core.DTOs.FacturacionMiscelaneos;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.FacturacionMiscelaneos;

public class FacturacionMiscelaneosService : IFacturacionMiscelaneosService
{
    private const string ContabilidadModuloVentas = "VENTAS";
    private const string ContabilidadDocumentoMiscelaneo = "MIS";
    private const string ContabilidadTipoTransaccionFacturacion = "FAC";

    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public FacturacionMiscelaneosService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<IReadOnlyList<ClienteLookupDto>> BuscarClientesAsync(string? query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<ClienteLookupDto>();
        }

        var filtro = $"%{query.Trim()}%";

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, filtro) ||
                EF.Functions.ILike(c.maestro_cliente_nombre, filtro) ||
                (c.maestro_cliente_rtn != null && EF.Functions.ILike(c.maestro_cliente_rtn, filtro)))
            .OrderBy(c => c.maestro_cliente_clave)
            .Take(20)
            .Select(c => new ClienteLookupDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault(),
                Rtn = c.maestro_cliente_rtn
            })
            .ToListAsync(ct);
    }

    public async Task<ClienteMiscelaneoDto?> ObtenerClienteAsync(string clienteClave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clienteClave))
        {
            return null;
        }

        var clave = clienteClave.Trim();

        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_clave == clave)
            .Select(c => new ClienteMiscelaneoDto
            {
                Clave = c.maestro_cliente_clave,
                Nombre = c.maestro_cliente_nombre,
                Rtn = c.maestro_cliente_rtn,
                Direccion = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<MiscelaneoCatalogoDto>> ListarCatalogoAsync(CancellationToken ct = default)
    {
        return await _context.miscelaneos_catalogos
            .AsNoTracking()
            .OrderBy(c => c.nombre)
            .Select(c => new MiscelaneoCatalogoDto
            {
                Id = c.ide,
                Codigo = c.codigo ?? string.Empty,
                Nombre = c.nombre ?? string.Empty,
                ValorUnitario = c.valor ?? 0m,
                ContAccountId = c.cont_account_id,
                CuentaContableDisplay = c.cont_account != null
                    ? c.cont_account.code + " - " + c.cont_account.name
                    : null
            })
            .ToListAsync(ct);
    }

    public async Task<MiscelaneoCatalogoEditDto?> ObtenerCatalogoItemAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return null;

        return await _context.miscelaneos_catalogos
            .AsNoTracking()
            .Where(c => c.ide == id)
            .Select(c => new MiscelaneoCatalogoEditDto
            {
                Id = c.ide,
                Codigo = c.codigo ?? string.Empty,
                Nombre = c.nombre ?? string.Empty,
                ValorUnitario = c.valor ?? 0m,
                ContAccountId = c.cont_account_id
            })
            .FirstOrDefaultAsync(ct);
    }

    public async Task<MiscelaneoCatalogoEditDto> CrearCatalogoItemAsync(MiscelaneoCatalogoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = (dto.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El codigo es obligatorio.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.");

        var duplicado = await _context.miscelaneos_catalogos
            .AsNoTracking()
            .AnyAsync(c => c.codigo != null && c.codigo.ToUpper() == codigo, ct);

        if (duplicado)
            throw new InvalidOperationException($"Ya existe un concepto con codigo '{codigo}'.");

        var entity = new miscelaneos_catalogo
        {
            codigo = codigo,
            nombre = nombre,
            valor = dto.ValorUnitario,
            cont_account_id = dto.ContAccountId
        };

        _context.miscelaneos_catalogos.Add(entity);
        await _context.SaveChangesAsync(ct);

        dto.Id = entity.ide;
        dto.Codigo = codigo;
        dto.Nombre = nombre;
        return dto;
    }

    public async Task<MiscelaneoCatalogoEditDto> ActualizarCatalogoItemAsync(int id, MiscelaneoCatalogoEditDto dto, string user, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (id <= 0)
            throw new ArgumentOutOfRangeException(nameof(id));

        var entity = await _context.miscelaneos_catalogos.FirstOrDefaultAsync(c => c.ide == id, ct);
        if (entity is null)
            throw new KeyNotFoundException("El concepto de catalogo no existe.");

        var codigo = (dto.Codigo ?? string.Empty).Trim().ToUpperInvariant();
        var nombre = (dto.Nombre ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("El codigo es obligatorio.");
        if (string.IsNullOrWhiteSpace(nombre))
            throw new ArgumentException("El nombre es obligatorio.");

        var duplicado = await _context.miscelaneos_catalogos
            .AsNoTracking()
            .AnyAsync(c => c.ide != id && c.codigo != null && c.codigo.ToUpper() == codigo, ct);

        if (duplicado)
            throw new InvalidOperationException($"Ya existe otro concepto con codigo '{codigo}'.");

        entity.codigo = codigo;
        entity.nombre = nombre;
        entity.valor = dto.ValorUnitario;
        entity.cont_account_id = dto.ContAccountId;

        await _context.SaveChangesAsync(ct);

        dto.Id = entity.ide;
        dto.Codigo = codigo;
        dto.Nombre = nombre;
        return dto;
    }

    public async Task<bool> EliminarCatalogoItemAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0) return false;

        var entity = await _context.miscelaneos_catalogos.FirstOrDefaultAsync(c => c.ide == id, ct);
        if (entity is null) return false;

        _context.miscelaneos_catalogos.Remove(entity);
        await _context.SaveChangesAsync(ct);
        return true;
    }

    public async Task<ResponseModelDto> CrearReciboAsync(FacturaMiscelaneoCrearDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return ResponseModelDto.Fail("La clave del cliente es requerida.");
        }

        var detallesValidos = dto.Detalles?
            .Where(d => d != null && !string.IsNullOrWhiteSpace(d.Codigo))
            .Select(d => new MiscelaneoDetalleDto
            {
                Codigo = d.Codigo.Trim(),
                Nombre = string.IsNullOrWhiteSpace(d.Nombre) ? d.Codigo.Trim() : d.Nombre.Trim(),
                Unidad = d.Unidad <= 0 ? 1 : d.Unidad,
                ValorUnitario = d.ValorUnitario,
                ValorTotal = d.ValorTotal > 0 ? d.ValorTotal : d.Unidad * d.ValorUnitario
            })
            .Where(d => d.ValorTotal > 0)
            .ToList() ?? new List<MiscelaneoDetalleDto>();

        if (detallesValidos.Count == 0)
        {
            return ResponseModelDto.Fail("Debe agregar al menos un concepto misceláneo con valores mayores a cero.");
        }

        var cliente = await ObtenerClienteAsync(dto.ClienteClave, ct);
        if (cliente is null)
        {
            return ResponseModelDto.Fail("El cliente indicado no existe.");
        }

        // Validar que cada código exista en el catálogo y tenga cuenta contable
        var codigosRecibidos = detallesValidos.Select(d => d.Codigo).Distinct().ToList();
        var catalogoConCuenta = await _context.miscelaneos_catalogos
            .AsNoTracking()
            .Where(c => c.codigo != null && codigosRecibidos.Contains(c.codigo))
            .Select(c => new { Codigo = c.codigo!, c.cont_account_id })
            .ToListAsync(ct);

        var codigosFaltantes = codigosRecibidos.Except(catalogoConCuenta.Select(c => c.Codigo)).ToList();
        if (codigosFaltantes.Count > 0)
        {
            return ResponseModelDto.Fail($"Los siguientes códigos no existen en el catálogo: {string.Join(", ", codigosFaltantes)}");
        }

        var sinCuenta = catalogoConCuenta.Where(c => !c.cont_account_id.HasValue).Select(c => c.Codigo).ToList();
        if (sinCuenta.Count > 0)
        {
            return ResponseModelDto.Fail($"Los siguientes conceptos no tienen cuenta contable configurada: {string.Join(", ", sinCuenta)}");
        }

        var cuentaPorCodigo = catalogoConCuenta.ToDictionary(c => c.Codigo, c => c.cont_account_id!.Value);

        var periodo = !string.IsNullOrWhiteSpace(dto.Periodo)
            ? dto.Periodo.Trim()
            : await ObtenerPeriodoActualAsync(ct);

        var usuario = string.IsNullOrWhiteSpace(dto.Usuario) ? "system" : dto.Usuario.Trim();
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var total = detallesValidos.Sum(d => d.ValorTotal);

        await using var tx = await _context.Database.BeginTransactionAsync(ct);

        var companyId = EnsureCompanyId();
        var factura = new factura
        {
            company_id = companyId,
            numfactura = string.Empty,
            clientecodigo = cliente.Clave,
            tipofactura = "R",
            ano = hoy.Year.ToString(),
            mes = hoy.Month.ToString(),
            fechaemision = hoy,
            fechavence = hoy,
            rtn = dto.Rtn ?? cliente.Rtn,
            periodo = periodo,
            numdei = string.Empty,
            saldototal = total,
            usuario = usuario,
            identidad = null,
            estado = "A"
        };

        _context.facturas.Add(factura);
        await _context.SaveChangesAsync(ct);

        var facturaDetalles = detallesValidos.Select(d => new factura_detalle
        {
            company_id = companyId,
            factura_id = factura.id,
            numrecibo = factura.numrecibo,
            codigo = d.Codigo,
            descripcion = d.Nombre,
            tiposervicio = "MISC",
            montovalor = d.ValorTotal,
            montovalor_saldo = d.ValorTotal
        }).ToList();

        _context.factura_detalles.AddRange(facturaDetalles);

        var saldoActual = await ObtenerSaldoClienteAsync(cliente.Clave, ct);
        var transacciones = new List<transaccion_abonado>();

        foreach (var detalle in detallesValidos)
        {
            saldoActual += detalle.ValorTotal;
            transacciones.Add(new transaccion_abonado
            {
                company_id = companyId,
                cliente_clave = cliente.Clave,
                recibo = factura.numrecibo,
                tipotransaccion = detalle.Codigo,
                docufuente = factura.id,
                docufuente2 = factura.numfactura,
                fecha_docu = hoy,
                tipo_partida = "01",
                descripcion = detalle.Nombre,
                debitos = detalle.ValorTotal,
                creditos = 0,
                saldo = saldoActual,
                tipo_servicio = "E",
                periodo = periodo,
                estado = "A",
                fecha_registro = hoy,
                usuario = usuario,
                saldo_detalle = detalle.ValorTotal
            });
        }

        if (transacciones.Count > 0)
        {
            _context.transaccion_abonados.AddRange(transacciones);
        }

        await _context.SaveChangesAsync(ct);

        // === Contabilidad automática: generar y postear póliza VENTAS/MIS ===
        var contabilidadDetails = detallesValidos.Select(d => new
        {
            code = d.Codigo,
            description = d.Nombre,
            account_id = cuentaPorCodigo[d.Codigo],
            total = d.ValorTotal
        }).ToArray();

        var pValues = JsonSerializer.Serialize(new
        {
            total,
            details = contabilidadDetails
        });

        var connection = _context.Database.GetDbConnection();
        var dbTransaction = _context.Database.CurrentTransaction?.GetDbTransaction();

        var typeId = await ResolverTipoTransaccionContableAsync(
            connection,
            companyId,
            ContabilidadTipoTransaccionFacturacion,
            dbTransaction,
            ct);

        const string spSql = @"
            SELECT public.sp_con_generar_comprobante(
                @CompanyId,
                @Module,
                @DocumentType,
                @DocumentId,
                @DocumentNumber,
                @PolizaDate::date,
                @Description,
                @Usuario,
                NULL,
                @TypeId,
                NULL,
                @ValuesJson::jsonb,
                false
            );
        ";

        var polizaId = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                spSql,
                new
                {
                    CompanyId = companyId,
                    Module = ContabilidadModuloVentas,
                    DocumentType = ContabilidadDocumentoMiscelaneo,
                    DocumentId = (long)factura.numrecibo,
                    DocumentNumber = $"MIS-{factura.numrecibo}",
                    PolizaDate = hoy.ToDateTime(TimeOnly.MinValue),
                    Description = $"Recibo miscelaneo {factura.numrecibo} - {cliente.Nombre}",
                    Usuario = usuario,
                    TypeId = typeId,
                    ValuesJson = pValues
                },
                transaction: dbTransaction,
                cancellationToken: ct));

        if (!polizaId.HasValue || polizaId.Value <= 0)
        {
            throw new InvalidOperationException(
                $"No se obtuvo poliza_id al generar comprobante contable para recibo miscelaneo {factura.numrecibo}.");
        }

        await tx.CommitAsync(ct);

        return ResponseModelDto.Ok(
            new
            {
                factura.id,
                factura.numrecibo,
                factura.clientecodigo,
                Total = total
            },
            "Recibo misceláneo generado correctamente.");
    }

    public async Task<FacturaMiscelaneoResponseDto?> ObtenerReciboAsync(int numeroRecibo, CancellationToken ct = default)
    {
        var recibo = await _context.facturas
            .AsNoTracking()
            .Where(f => f.numrecibo == numeroRecibo)
            .Select(f => new FacturaMiscelaneoResponseDto
            {
                FacturaId = f.id,
                NumeroRecibo = f.numrecibo,
                NumFactura = f.numfactura ?? string.Empty,
                ClienteClave = f.clientecodigo ?? string.Empty,
                FechaEmision = f.fechaemision.HasValue
                    ? f.fechaemision.Value.ToDateTime(TimeOnly.MinValue)
                    : DateTime.MinValue,
                Total = f.saldototal ?? 0m
            })
            .FirstOrDefaultAsync(ct);

        if (recibo is null)
        {
            return null;
        }

        var detalles = await _context.factura_detalles
            .AsNoTracking()
            .Where(d => d.factura_id == recibo.FacturaId)
            .OrderBy(d => d.id)
            .Select(d => new MiscelaneoDetalleDto
            {
                Codigo = d.codigo ?? string.Empty,
                Nombre = d.descripcion ?? string.Empty,
                Unidad = 1,
                ValorUnitario = d.montovalor ?? 0m,
                ValorTotal = d.montovalor ?? 0m
            })
            .ToListAsync(ct);

        recibo.Detalles = detalles;

        return recibo;
    }

    private async Task<string> ObtenerPeriodoActualAsync(CancellationToken ct)
    {
        var periodo = await _context.historialmes
            .AsNoTracking()
            .Where(p => p.cerrarperiodo == 'P')
            .OrderByDescending(p => p.ano)
            .ThenByDescending(p => p.mes)
            .Select(p => new { p.ano, p.mes })
            .FirstOrDefaultAsync(ct);

        if (periodo is null)
        {
            return DateTime.UtcNow.ToString("yyyyMM");
        }

        var ano = Convert.ToInt32(periodo.ano);
        var mes = Convert.ToInt32(periodo.mes);
        return $"{ano:D4}{mes:D2}";
    }

    private async Task<decimal> ObtenerSaldoClienteAsync(string clienteClave, CancellationToken ct)
    {
        var saldo = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clienteClave)
            .OrderByDescending(t => t.fecha_docu)
            .ThenByDescending(t => t.ide)
            .Select(t => t.saldo ?? 0m)
            .FirstOrDefaultAsync(ct);

        return saldo;
    }

    private long EnsureCompanyId()
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo resolver la empresa activa para facturacion miscelaneos.");
        }

        return companyId;
    }

    private static async Task<long> ResolverTipoTransaccionContableAsync(
        IDbConnection connection,
        long companyId,
        string transactionCode,
        IDbTransaction? transaction,
        CancellationToken ct)
    {
        const string sql = @"
            SELECT t.type_id
            FROM public.con_tipo_transaccion t
            WHERE t.company_id = @CompanyId
              AND upper(COALESCE(t.code, '')) = @TransactionCode
              AND COALESCE(
                    t.status_id,
                    CASE
                        WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('ACTIVE', 'ACTIVO') THEN 1
                        WHEN upper(COALESCE(t.status, 'ACTIVE')) IN ('INACTIVE', 'INACTIVO') THEN 0
                        ELSE 1
                    END
                  ) = 1
            ORDER BY t.type_id
            LIMIT 1;
        ";

        var typeId = await connection.ExecuteScalarAsync<long?>(
            new CommandDefinition(
                sql,
                new
                {
                    CompanyId = companyId,
                    TransactionCode = transactionCode.ToUpperInvariant()
                },
                transaction: transaction,
                cancellationToken: ct));

        if (!typeId.HasValue || typeId.Value <= 0)
        {
            throw new InvalidOperationException(
                $"No existe con_tipo_transaccion activo para company_id={companyId} con code={transactionCode}.");
        }

        return typeId.Value;
    }
}

