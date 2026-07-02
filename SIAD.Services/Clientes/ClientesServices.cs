using System;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using Dapper;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using SIAD.Core.DTOs.Clientes;
using SIAD.Core.DTOs.Common;
using SIAD.Core.Entities;
using SIAD.Core.Tenancy;
using SIAD.Data;

namespace SIAD.Services.Clientes;

public class ClientesService : IClientesService
{
    private readonly SiadDbContext _context;
    private readonly ICurrentCompanyService _currentCompanyService;

    public ClientesService(SiadDbContext context, ICurrentCompanyService currentCompanyService)
    {
        _context = context;
        _currentCompanyService = currentCompanyService;
    }

    public async Task<ClienteCreateResponseDto> CrearClienteAsync(ClienteCreateDto dto, string usuarioCreacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(usuarioCreacion);

        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        var identidad = Limpiar(dto.Dni);
        if (string.IsNullOrWhiteSpace(identidad))
        {
            throw new ArgumentException("El DNI del cliente es obligatorio.", nameof(dto.Dni));
        }

        var nombreCompleto = ConstruirNombreCompleto(dto.Nombre, dto.Apellidos);
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(dto.Nombre));
        }

        if (await _context.cliente_maestros.AnyAsync(c => c.maestro_cliente_identidad == identidad, ct))
        {
            throw new InvalidOperationException($"Ya existe un cliente con el DNI {identidad}.");
        }

        var clave = Limpiar(dto.Clave);
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new ArgumentException("El código del sistema es obligatorio.", nameof(dto.Clave));
        }

        if (await _context.cliente_maestros.AnyAsync(c => c.maestro_cliente_clave == clave, ct))
        {
            throw new InvalidOperationException($"Ya existe un cliente con el código {clave}.");
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var indicativoRuta = ConstruirIndicativoRuta(dto.CicloId, dto.BarrioCodigo, dto.Libreta, dto.Secuencia);

        var maestro = new cliente_maestro
        {
            maestro_cliente_clave = clave!,
            maestro_cliente_identidad = identidad,
            maestro_cliente_rtn = Limpiar(dto.Rtn),
            maestro_cliente_nombre = nombreCompleto,
            maestro_cliente_tercera_edad = dto.TerceraEdad,
            categoria_servicio_id = dto.CategoriaServicioId,
            barrio_codigo = Limpiar(dto.BarrioCodigo),
            maestro_cliente_indicativo_ruta = indicativoRuta,
            maestro_cliente_secuencia = Limpiar(dto.Secuencia),
            estado = dto.Activo,
            usuariocreacion = usuarioCreacion,
            fechacreacion = ahora,
            tipo_uso_codigo = Limpiar(dto.TipoUsoCodigo),
            ciclos_id = dto.CicloId,
            cliente_fecha_nac = dto.FechaNacimiento?.Date,
            maestro_cliente_tiene_contrato = dto.TieneContrato,
            maestro_cliente_tiene_convenio = dto.TieneConvenio,
            maestro_cliente_tiene_medidor = dto.TieneMedidor,
            clave_sure = Limpiar(dto.ClaveSure),
            contador = Limpiar(dto.Contador),
            letracodigo = Limpiar(dto.LetraCodigo),
            bloqueado_cobranza = dto.BloqueadoCobranza,
            abogado = dto.AbogadoId
        };

        var detalle = new cliente_detalle
        {
            maestro_cliente = maestro,
            detalle_cliente_telefono = Limpiar(dto.Telefono),
            detalle_cliente_movil = Limpiar(dto.TelefonoMovil),
            detalle_cliente_email = Limpiar(dto.Email),
            detalle_cliente_direccion = Limpiar(dto.Direccion),
            detalle_cliente_color_casa = Limpiar(dto.ColorCasa),
            maestro_medidor_id = dto.MedidorId,
            empresa_nombre = Limpiar(dto.EmpresaNombre),
            empresa_telefono = Limpiar(dto.EmpresaTelefono),
            empresa_direccion = Limpiar(dto.EmpresaDireccion),
            clave = Limpiar(dto.EmpresaRtn),
            negocio_clave_catastral = Limpiar(dto.ClaveCatastral),
            observaciones = ConstruirObservaciones(dto.Observaciones, dto.NumeroConvenio),
            numero_contrato = Limpiar(dto.NumeroContrato),
            estado = dto.Activo,
            usuariocreacion = usuarioCreacion,
            fechacreacion = ahora
        };

        maestro.cliente_detalles.Add(detalle);
        _context.cliente_maestros.Add(maestro);

        try
        {
            await _context.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsClienteClaveUniqueViolation(ex))
        {
            throw new InvalidOperationException($"Ya existe un cliente con el código {clave}.", ex);
        }

        return new ClienteCreateResponseDto
        {
            Id = maestro.maestro_cliente_id,
            Codigo = clave!
        };
    }

    public async Task<ClienteDetailDto> ActualizarClienteAsync(int id, ClienteUpdateDto dto, string usuarioModificacion, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(usuarioModificacion);

        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        var maestro = await _context.cliente_maestros
            .Include(c => c.cliente_detalles)
            .FirstOrDefaultAsync(c => c.maestro_cliente_id == id, ct);

        if (maestro is null)
        {
            throw new KeyNotFoundException("Cliente no encontrado.");
        }

        var clave = Limpiar(dto.Clave);
        if (string.IsNullOrWhiteSpace(clave))
        {
            throw new ArgumentException("La clave del cliente es obligatoria.", nameof(dto.Clave));
        }

        if (!string.Equals(maestro.maestro_cliente_clave, clave, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("No se permite modificar la clave del cliente.");
        }

        var identidad = Limpiar(dto.Dni);
        if (string.IsNullOrWhiteSpace(identidad))
        {
            throw new ArgumentException("El DNI del cliente es obligatorio.", nameof(dto.Dni));
        }

        if (!string.Equals(maestro.maestro_cliente_identidad, identidad, StringComparison.OrdinalIgnoreCase))
        {
            var existeIdentidad = await _context.cliente_maestros
                .AnyAsync(c => c.maestro_cliente_identidad == identidad && c.maestro_cliente_id != id, ct);
            if (existeIdentidad)
            {
                throw new InvalidOperationException($"Ya existe un cliente con el DNI {identidad}.");
            }
        }

        var nombreCompleto = ConstruirNombreCompleto(dto.Nombre, dto.Apellidos);
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            throw new ArgumentException("El nombre del cliente es obligatorio.", nameof(dto.Nombre));
        }

        var ahora = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var indicativoRuta = ConstruirIndicativoRuta(dto.CicloId, dto.BarrioCodigo, dto.Libreta, dto.Secuencia);
        var numeroConvenio = dto.TieneConvenio == true ? dto.NumeroConvenio : null;
        var numeroContrato = dto.TieneContrato == true ? dto.NumeroContrato : null;

        maestro.maestro_cliente_identidad = identidad;
        maestro.maestro_cliente_rtn = Limpiar(dto.Rtn);
        maestro.maestro_cliente_nombre = nombreCompleto;
        maestro.maestro_cliente_tercera_edad = dto.TerceraEdad;
        maestro.categoria_servicio_id = dto.CategoriaServicioId;
        maestro.barrio_codigo = Limpiar(dto.BarrioCodigo);
        maestro.maestro_cliente_indicativo_ruta = indicativoRuta;
        maestro.maestro_cliente_secuencia = Limpiar(dto.Secuencia);
        maestro.estado = dto.Activo;
        maestro.usuariomodificacion = usuarioModificacion;
        maestro.fechamodificacion = ahora;
        maestro.tipo_uso_codigo = Limpiar(dto.TipoUsoCodigo);
        maestro.ciclos_id = dto.CicloId;
        maestro.cliente_fecha_nac = dto.FechaNacimiento?.Date;
        maestro.maestro_cliente_tiene_contrato = dto.TieneContrato;
        maestro.maestro_cliente_tiene_convenio = dto.TieneConvenio;
        maestro.maestro_cliente_tiene_medidor = dto.TieneMedidor;
        maestro.clave_sure = Limpiar(dto.ClaveSure);
        maestro.contador = Limpiar(dto.Contador);
        maestro.letracodigo = Limpiar(dto.LetraCodigo);
        maestro.bloqueado_cobranza = dto.BloqueadoCobranza;
        maestro.abogado = dto.AbogadoId;

        var detalle = maestro.cliente_detalles
            .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
            .FirstOrDefault();

        if (detalle is null)
        {
            detalle = new cliente_detalle
            {
                maestro_cliente = maestro,
                usuariocreacion = usuarioModificacion,
                fechacreacion = ahora,
                estado = dto.Activo
            };
            maestro.cliente_detalles.Add(detalle);
        }
        else
        {
            detalle.usuariomodificacion = usuarioModificacion;
            detalle.fechamodificacion = ahora;
            detalle.estado = dto.Activo;
        }

        detalle.detalle_cliente_telefono = Limpiar(dto.Telefono);
        detalle.detalle_cliente_movil = Limpiar(dto.TelefonoMovil);
        detalle.detalle_cliente_email = Limpiar(dto.Email);
        detalle.detalle_cliente_direccion = Limpiar(dto.Direccion);
        detalle.detalle_cliente_color_casa = Limpiar(dto.ColorCasa);
        detalle.maestro_medidor_id = dto.TieneMedidor == true ? dto.MedidorId : null;
        detalle.empresa_nombre = Limpiar(dto.EmpresaNombre);
        detalle.empresa_telefono = Limpiar(dto.EmpresaTelefono);
        detalle.empresa_direccion = Limpiar(dto.EmpresaDireccion);
        detalle.clave = Limpiar(dto.EmpresaRtn);
        detalle.negocio_clave_catastral = Limpiar(dto.ClaveCatastral);
        detalle.observaciones = ConstruirObservaciones(dto.Observaciones, numeroConvenio);
        detalle.numero_contrato = Limpiar(numeroContrato);

        await _context.SaveChangesAsync(ct);

        var actualizado = await GetClienteAsync(id, ct);
        if (actualizado is null)
        {
            throw new KeyNotFoundException("Cliente no encontrado.");
        }

        return actualizado;
    }
    public async Task<IReadOnlyList<ClienteListItemDto>> SearchClientesAsync(ClienteFilterDto filtro, CancellationToken cancellationToken = default)
    {
        var clientes = _context.cliente_maestros.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Codigo))
        {
            var patron = $"%{filtro.Codigo.Trim()}%";
            clientes = clientes.Where(c => EF.Functions.ILike(c.maestro_cliente_clave, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Nombre))
        {
            var patron = $"%{filtro.Nombre.Trim()}%";
            clientes = clientes.Where(c => EF.Functions.ILike(c.maestro_cliente_nombre, patron));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Barrio))
        {
            var patron = $"%{filtro.Barrio.Trim()}%";
            clientes = clientes.Where(c => c.barrio_codigo != null && EF.Functions.ILike(c.barrio_codigo, patron));
        }

        if (filtro.SoloActivos)
        {
            clientes = clientes.Where(c => c.estado);
        }

        return await clientes
            .Select(c => new ClienteListItemDto(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.maestro_cliente_identidad,
                c.barrio_codigo,
                c.estado,
                c.ciclos != null ? c.ciclos.ciclos_codigo : null,
                c.maestro_cliente_indicativo_ruta))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<ClienteListItemDto>> SearchClientesPagedAsync(string? search, bool soloActivos, int skip, int take, string? sortField, bool sortDesc, CancellationToken cancellationToken = default)
    {
        var query = _context.cliente_maestros.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var patron = $"%{search.Trim()}%";
            query = query.Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, patron) ||
                EF.Functions.ILike(c.maestro_cliente_nombre, patron) ||
                (c.maestro_cliente_identidad != null && EF.Functions.ILike(c.maestro_cliente_identidad, patron)) ||
                (c.barrio_codigo != null && EF.Functions.ILike(c.barrio_codigo, patron)));
        }

        if (soloActivos)
        {
            query = query.Where(c => c.estado);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        skip = Math.Max(skip, 0);
        take = take <= 0 ? 50 : take;

        query = ApplySorting(query, sortField, sortDesc);

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(c => new ClienteListItemDto(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.maestro_cliente_identidad,
                c.barrio_codigo,
                c.estado,
                c.ciclos != null ? c.ciclos.ciclos_codigo : null,
                c.maestro_cliente_indicativo_ruta))
            .ToListAsync(cancellationToken);

        return new PagedResult<ClienteListItemDto>(items, totalCount);
    }


    public async Task<IReadOnlyList<ClienteListItemDto>> GetClientesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.cliente_maestros
            .AsNoTracking()
            .Select(c => new ClienteListItemDto(
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.maestro_cliente_identidad,
                c.barrio_codigo,
                c.estado,
                c.ciclos != null ? c.ciclos.ciclos_codigo : null,
                c.maestro_cliente_indicativo_ruta))
            .ToListAsync(cancellationToken);
    }

    public async Task<ClienteDetailDto?> GetClienteAsync(int id, CancellationToken cancellationToken = default)
    {
        var raw = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == id)
            .Select(c => new
            {
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.maestro_cliente_identidad,
                c.maestro_cliente_rtn,
                c.cliente_fecha_nac,
                c.maestro_cliente_tercera_edad,
                c.bloqueado_cobranza,
                c.abogado,
                AbogadoNombre = _context.abogados
                    .Where(a => a.abogado_id == c.abogado)
                    .Select(a => a.abogado_nombrecorto)
                    .FirstOrDefault(),
                c.barrio_codigo,
                BarrioNombre = c.barrio_codigoNavigation != null ? c.barrio_codigoNavigation.descripcion : null,
                c.tipo_uso_codigo,
                c.categoria_servicio_id,
                CategoriaServicioNombre = c.categoria_servicio != null ? c.categoria_servicio.descripcion : null,
                c.letracodigo,
                c.ciclos_id,
                CicloDescripcion = c.ciclos != null ? c.ciclos.ciclos_descripcioncorta : null,
                c.maestro_cliente_indicativo_ruta,
                c.maestro_cliente_secuencia,
                c.clave_sure,
                c.contador,
                c.maestro_cliente_tiene_medidor,
                c.maestro_cliente_tiene_convenio,
                c.maestro_cliente_tiene_contrato,
                c.estado,
                c.usuariocreacion,
                c.fechacreacion,
                c.descuento_tercera_edad,
                c.maestro_cliente_estudio_socioeconomico,
                c.no_cortable,
                Detalle = c.cliente_detalles
                    .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                    .Select(d => new
                    {
                        d.detalle_cliente_telefono,
                        d.detalle_cliente_movil,
                        d.detalle_cliente_email,
                        d.detalle_cliente_direccion,
                        d.detalle_cliente_color_casa,
                        d.maestro_medidor_id,
                        MedidorNumero = d.maestro_medidor != null ? d.maestro_medidor.maestro_medidor_numero : null,
                        d.empresa_nombre,
                        EmpresaRtn = d.clave,
                        d.empresa_telefono,
                        d.empresa_direccion,
                        d.negocio_clave_catastral,
                        d.observaciones,
                        d.numero_contrato
                    })
                    .FirstOrDefault()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (raw is null)
        {
            return null;
        }

        SepararNombreApellidos(raw.maestro_cliente_nombre, out var nombre, out var apellidos);
        var (numeroConvenio, observacionesLimpias) = ExtraerConvenio(raw.Detalle?.observaciones);

        return new ClienteDetailDto
        {
            Id = raw.maestro_cliente_id,
            Codigo = raw.maestro_cliente_clave,
            Nombre = nombre,
            Apellidos = apellidos,
            Dni = raw.maestro_cliente_identidad,
            Rtn = raw.maestro_cliente_rtn,
            FechaNacimiento = raw.cliente_fecha_nac,
            TerceraEdad = raw.maestro_cliente_tercera_edad,
            BloqueadoCobranza = raw.bloqueado_cobranza,
            AbogadoId = raw.abogado,
            AbogadoNombre = raw.AbogadoNombre,
            Telefono = raw.Detalle?.detalle_cliente_telefono,
            TelefonoMovil = raw.Detalle?.detalle_cliente_movil,
            Email = raw.Detalle?.detalle_cliente_email,
            Direccion = raw.Detalle?.detalle_cliente_direccion,
            BarrioCodigo = raw.barrio_codigo,
            BarrioNombre = raw.BarrioNombre,
            ColorCasa = raw.Detalle?.detalle_cliente_color_casa,
            EmpresaNombre = raw.Detalle?.empresa_nombre,
            EmpresaRtn = raw.Detalle?.EmpresaRtn,
            EmpresaTelefono = raw.Detalle?.empresa_telefono,
            EmpresaDireccion = raw.Detalle?.empresa_direccion,
            ServicioId = null,
            ServicioNombre = null,
            CategoriaServicioId = raw.categoria_servicio_id,
            CategoriaServicioNombre = raw.CategoriaServicioNombre,
            LetraCodigo = raw.letracodigo,
            CicloId = raw.ciclos_id,
            CicloDescripcion = raw.CicloDescripcion,
            Libreta = ExtraerLibreta(raw.maestro_cliente_indicativo_ruta),
            IndicativoRuta = raw.maestro_cliente_indicativo_ruta,
            Secuencia = raw.maestro_cliente_secuencia,
            TipoUsoCodigo = raw.tipo_uso_codigo,
            ClaveCatastral = raw.Detalle?.negocio_clave_catastral,
            ClaveSure = raw.clave_sure,
            Contador = raw.contador,
            TieneMedidor = raw.maestro_cliente_tiene_medidor,
            MedidorId = raw.Detalle?.maestro_medidor_id,
            MedidorNumero = raw.Detalle?.MedidorNumero,
            TieneConvenio = raw.maestro_cliente_tiene_convenio,
            NumeroConvenio = numeroConvenio,
            TieneContrato = raw.maestro_cliente_tiene_contrato,
            NumeroContrato = raw.Detalle?.numero_contrato,
            Observaciones = observacionesLimpias,
            UsuarioCreacion = raw.usuariocreacion,
            FechaCreacion = raw.fechacreacion,
            DescuentoTerceraEdad = raw.descuento_tercera_edad,
            EstudioSocioeconomico = raw.maestro_cliente_estudio_socioeconomico,
            NoCortable = raw.no_cortable,
            Activo = raw.estado
        };
    }
    public async Task<ClienteEstadoCuentaDto> GetEstadoCuentaAsync(int clienteId, CancellationToken ct = default)
    {
        var companyId = _currentCompanyService.GetCompanyId();
        if (companyId <= 0)
        {
            throw new InvalidOperationException("No se pudo determinar la empresa (tenant) actual.");
        }

        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return new ClienteEstadoCuentaDto(null, null, null, null, null, Array.Empty<SaldoServicioDto>());
        }

        // Saldo total acumulado: SP es la unica fuente de verdad (ORDER BY ide DESC
        // sobre transaccion_abonado activo). El query EF anterior usaba fecha_docu DESC
        // que es ambiguo cuando una factura V3 inserta 4 rows (uno por servicio) con
        // misma fecha — devolvia un saldo parcial en lugar del total.
        var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sqlSaldo = "SELECT saldo_actual FROM public.sp_obtener_cliente_saldo(@CompanyId, @Clave)";
        var saldoActual = await connection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(sqlSaldo,
                new { CompanyId = companyId, Clave = clave },
                cancellationToken: ct)) ?? 0m;

        var ultimoPago = await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.company_id == companyId
                        && t.cliente_clave == clave
                        && t.tipotransaccion != null
                        && EF.Functions.ILike(t.tipotransaccion, "%PAGO%"))
            .OrderByDescending(t => t.ide)
            .Select(t => new { t.fecha_docu, t.creditos, t.debitos })
            .FirstOrDefaultAsync(ct);

        DateTime? fechaPago = ultimoPago?.fecha_docu.HasValue == true
            ? ultimoPago.fecha_docu.Value.ToDateTime(TimeOnly.MinValue)
            : null;
        decimal? montoPago = ultimoPago?.creditos ?? ultimoPago?.debitos ?? 0m;

        var consumos = await _context.historicomedicions
            .AsNoTracking()
            .Where(h => h.clave == clave && h.consumo.HasValue)
            .OrderByDescending(h => h.fecha)
            .Select(h => h.consumo!.Value)
            .Take(6)
            .ToArrayAsync(ct);

        decimal? consumoPromedio = consumos.Length > 0 ? consumos.Average() : 0m;

        // Desglose por servicio usando saldo_detalle del movimiento más reciente por tipo_servicio.
        // JOIN con adm_servicio para obtener el nombre amigable y el orden visual del catálogo.
        const string sqlDesglose =
            "SELECT " +
            "  COALESCE(s.nombre, sub.tipo_servicio, 'Sin servicio') AS servicio, " +
            "  COALESCE(sub.saldo_detalle, 0)                        AS saldo, " +
            "  0                                                      AS meses_pendientes " +
            "FROM ( " +
            "  SELECT DISTINCT ON (ta.tipo_servicio) " +
            "    ta.tipo_servicio, ta.saldo_detalle " +
            "  FROM transaccion_abonado ta " +
            "  WHERE ta.company_id  = @CompanyId " +
            "    AND ta.cliente_clave = @Clave " +
            "    AND ta.tipo_servicio IS NOT NULL " +
            "    AND ta.estado = 'A' " +
            "  ORDER BY ta.tipo_servicio, ta.ide DESC " +
            ") sub " +
            "LEFT JOIN adm_servicio s " +
            "       ON s.codigo      = sub.tipo_servicio " +
            "      AND s.company_id  = @CompanyId " +
            "ORDER BY COALESCE(s.orden_visual, 999), servicio";

        var desgloseRows = await connection.QueryAsync<SaldoServicioRow>(
            new CommandDefinition(sqlDesglose,
                new { CompanyId = companyId, Clave = clave },
                cancellationToken: ct));

        var saldoPorServicio = desgloseRows
            .Select(r => new SaldoServicioDto(r.Servicio, r.Saldo, (int)r.MesesPendientes))
            .ToList()
            .AsReadOnly();

        return new ClienteEstadoCuentaDto(
            saldoActual,
            fechaPago,
            montoPago,
            consumoPromedio,
            null,
            saldoPorServicio);
    }

    private sealed class SaldoServicioRow
    {
        public string Servicio { get; init; } = string.Empty;
        public decimal Saldo { get; init; }
        public long MesesPendientes { get; init; }
    }

    public async Task<IReadOnlyList<ClienteMovimientoDto>> GetMovimientosAsync(int clienteId, CancellationToken ct = default)
    {
        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return Array.Empty<ClienteMovimientoDto>();
        }

        return await _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clave)
            .OrderByDescending(t => t.ide)
            .Select(t => new ClienteMovimientoDto(
                t.ide,
                t.fecha_docu.HasValue ? t.fecha_docu.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                t.tipotransaccion ?? string.Empty,
                t.descripcion,
                (t.creditos ?? 0) - (t.debitos ?? 0),
                t.saldo ?? 0,
                t.recibo,
                _context.facturas
                    .Where(f => f.numrecibo == t.recibo && f.clientecodigo == t.cliente_clave)
                    .Select(f => f.numfactura)
                    .FirstOrDefault()))
            .ToListAsync(ct);
    }

    public async Task<PagedResult<ClienteMovimientoDto>> GetMovimientosPagedAsync(
        int clienteId,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return new PagedResult<ClienteMovimientoDto>(Array.Empty<ClienteMovimientoDto>(), 0);
        }

        var query = _context.transaccion_abonados
            .AsNoTracking()
            .Where(t => t.cliente_clave == clave);

        var totalCount = await query.CountAsync(ct);
        query = ApplyMovimientosSort(query, sortField, sortDesc);

        if (skip < 0)
        {
            skip = 0;
        }

        if (take <= 0)
        {
            take = 50;
        }

        var items = await query
            .Skip(skip)
            .Take(take)
            .Select(t => new ClienteMovimientoDto(
                t.ide,
                t.fecha_docu.HasValue ? t.fecha_docu.Value.ToDateTime(TimeOnly.MinValue) : DateTime.MinValue,
                t.tipotransaccion ?? string.Empty,
                t.descripcion,
                (t.creditos ?? 0) - (t.debitos ?? 0),
                t.saldo ?? 0,
                t.recibo,
                _context.facturas
                    .Where(f => (decimal?)f.numrecibo == t.recibo && f.clientecodigo == t.cliente_clave)
                    .Select(f => f.numfactura)
                    .FirstOrDefault()))
            .ToListAsync(ct);

        return new PagedResult<ClienteMovimientoDto>(items, totalCount);
    }

    public async Task<ClienteHistoricoConsumoResponseDto> GetHistoricoConsumoAsync(int clienteId, DateTime desde, DateTime hasta, CancellationToken ct = default)
    {
        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return new ClienteHistoricoConsumoResponseDto(null, Array.Empty<ClienteHistoricoConsumoItemDto>());
        }

        var desdeDate = desde.Date;
        var hastaDate = hasta.Date;

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sqlHeader = @"
            SELECT
                r_cliente_clave AS ""CodigoCliente"",
                r_cliente_nombre AS ""NombreCliente"",
                r_cliente_direccion AS ""DireccionCliente"",
                r_cliente_medidor AS ""NumeroMedidor"",
                r_catastral AS ""Catastro"",
                r_diametro AS ""Diametro"",
                r_categoria AS ""Categoria"",
                '' AS ""TipoTarifa"",
                r_ciclo AS ""Ciclo"",
                r_ruta AS ""Ruta"",
                r_secuencia AS ""Secuencia""
            FROM sp_cliente_informacion(@p_codigocliente)";

        var headerRow = await connection.QueryFirstOrDefaultAsync<HistoricoConsumoHeaderRow>(
            new CommandDefinition(sqlHeader, new { p_codigocliente = clave.Trim() }, cancellationToken: ct));

        var header = headerRow is null
            ? null
            : new ClienteHistoricoConsumoHeaderDto(
                desdeDate,
                hastaDate,
                headerRow.CodigoCliente ?? clave,
                headerRow.NombreCliente ?? string.Empty,
                headerRow.DireccionCliente,
                headerRow.NumeroMedidor,
                headerRow.Catastro,
                headerRow.Diametro,
                headerRow.Categoria,
                headerRow.TipoTarifa,
                headerRow.Ciclo,
                headerRow.Ruta,
                headerRow.Secuencia);

        const string sqlDetalle = @"
            SELECT
                r_fecha_lect_ant AS ""FechaLectAnterior"",
                r_fecha_lect_act AS ""FechaLectActual"",
                r_lect_ant AS ""LecturaAnterior"",
                r_lect_act AS ""LecturaActual"",
                r_consumo AS ""Consumo"",
                r_ano AS ""Ano"",
                r_mes AS ""Mes"",
                r_total AS ""Total"",
                r_condicion AS ""Condicion"",
                r_tipolectura AS ""TipoLectura"",
                r_ajuste AS ""Ajuste"",
                r_observacion AS ""Observacion""
            FROM sp_obtener_historial_consumo(@p_codigocliente, @p_desde::date, @p_hasta::date)";

        var rows = await connection.QueryAsync<HistoricoConsumoRow>(
            new CommandDefinition(sqlDetalle, new
            {
                p_codigocliente = clave.Trim(),
                p_desde = desdeDate,
                p_hasta = hastaDate
            }, cancellationToken: ct));

        var items = rows
            .Select(r => new ClienteHistoricoConsumoItemDto(
                BuildPeriodo(r.Ano, r.Mes),
                FormatearFecha(r.FechaLectAnterior),
                FormatearFecha(r.FechaLectActual),
                r.LecturaAnterior ?? 0m,
                r.LecturaActual ?? 0m,
                r.Consumo ?? 0m,
                r.Total ?? 0m,
                r.Condicion,
                r.TipoLectura,
                r.Ajuste,
                r.Observacion))
            .ToList();

        return new ClienteHistoricoConsumoResponseDto(header, items);
    }

    // NOTE: Added paged historico consumo (count + sum + items) to support remote paging/sorting without loading all rows.
    public async Task<ClienteHistoricoConsumoPagedResponseDto> GetHistoricoConsumoPagedAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        int skip,
        int take,
        string? sortField,
        bool sortDesc,
        CancellationToken ct = default)
    {
        var clave = await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(clave))
        {
            return new ClienteHistoricoConsumoPagedResponseDto(null, Array.Empty<ClienteHistoricoConsumoItemDto>(), 0, 0m);
        }

        var desdeDate = desde.Date;
        var hastaDate = hasta.Date;
        if (desdeDate > hastaDate)
        {
            (desdeDate, hastaDate) = (hastaDate, desdeDate);
        }

        skip = Math.Max(skip, 0);
        take = take <= 0 ? 100 : take;
        if (take > 500)
        {
            take = 500;
        }

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sqlHeader = @"
            SELECT
                r_cliente_clave AS ""CodigoCliente"",
                r_cliente_nombre AS ""NombreCliente"",
                r_cliente_direccion AS ""DireccionCliente"",
                r_cliente_medidor AS ""NumeroMedidor"",
                r_catastral AS ""Catastro"",
                r_diametro AS ""Diametro"",
                r_categoria AS ""Categoria"",
                '' AS ""TipoTarifa"",
                r_ciclo AS ""Ciclo"",
                r_ruta AS ""Ruta"",
                r_secuencia AS ""Secuencia""
            FROM sp_cliente_informacion(@p_codigocliente)";

        var headerRow = await connection.QueryFirstOrDefaultAsync<HistoricoConsumoHeaderRow>(
            new CommandDefinition(sqlHeader, new { p_codigocliente = clave.Trim() }, cancellationToken: ct));

        var header = headerRow is null
            ? null
            : new ClienteHistoricoConsumoHeaderDto(
                desdeDate,
                hastaDate,
                headerRow.CodigoCliente ?? clave,
                headerRow.NombreCliente ?? string.Empty,
                headerRow.DireccionCliente,
                headerRow.NumeroMedidor,
                headerRow.Catastro,
                headerRow.Diametro,
                headerRow.Categoria,
                headerRow.TipoTarifa,
                headerRow.Ciclo,
                headerRow.Ruta,
                headerRow.Secuencia);

        const string sqlSummary = @"
            SELECT
                COUNT(*) AS ""TotalCount"",
                COALESCE(SUM(r_total), 0) AS ""TotalConsumo""
            FROM sp_obtener_historial_consumo(@p_codigocliente, @p_desde::date, @p_hasta::date)";

        var summary = await connection.QuerySingleAsync<HistoricoConsumoSummaryRow>(
            new CommandDefinition(sqlSummary, new
            {
                p_codigocliente = clave.Trim(),
                p_desde = desdeDate,
                p_hasta = hastaDate
            }, cancellationToken: ct));

        var orderBy = BuildHistoricoConsumoOrderBy(sortField, sortDesc);
        var sqlDetalle = $@"
            SELECT
                r_fecha_lect_ant AS ""FechaLectAnterior"",
                r_fecha_lect_act AS ""FechaLectActual"",
                r_lect_ant AS ""LecturaAnterior"",
                r_lect_act AS ""LecturaActual"",
                r_consumo AS ""Consumo"",
                r_ano AS ""Ano"",
                r_mes AS ""Mes"",
                r_total AS ""Total"",
                r_condicion AS ""Condicion"",
                r_tipolectura AS ""TipoLectura"",
                r_ajuste AS ""Ajuste"",
                r_observacion AS ""Observacion""
            FROM sp_obtener_historial_consumo(@p_codigocliente, @p_desde::date, @p_hasta::date)
            {orderBy}
            OFFSET @skip LIMIT @take";

        var rows = await connection.QueryAsync<HistoricoConsumoRow>(
            new CommandDefinition(sqlDetalle, new
            {
                p_codigocliente = clave.Trim(),
                p_desde = desdeDate,
                p_hasta = hastaDate,
                skip,
                take
            }, cancellationToken: ct));

        var items = rows
            .Select(r => new ClienteHistoricoConsumoItemDto(
                BuildPeriodo(r.Ano, r.Mes),
                FormatearFecha(r.FechaLectAnterior),
                FormatearFecha(r.FechaLectActual),
                r.LecturaAnterior ?? 0m,
                r.LecturaActual ?? 0m,
                r.Consumo ?? 0m,
                r.Total ?? 0m,
                r.Condicion,
                r.TipoLectura,
                r.Ajuste,
                r.Observacion))
            .ToList();

        return new ClienteHistoricoConsumoPagedResponseDto(header, items, summary.TotalCount, summary.TotalConsumo);
    }

    public async Task<ClienteFotoMedidorHeaderDto?> GetFotoMedidorHeaderAsync(int clienteId, CancellationToken ct = default)
    {
        var clave = await GetClienteClaveAsync(clienteId, ct);
        if (string.IsNullOrWhiteSpace(clave))
        {
            return null;
        }

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sql = @"
            SELECT
                r_cliente_clave AS ""CodigoCliente"",
                r_cliente_nombre AS ""NombreCliente"",
                r_cliente_direccion AS ""DireccionCliente"",
                r_cliente_medidor AS ""NumeroMedidor"",
                r_ciclo AS ""Ciclo"",
                r_ruta AS ""Ruta"",
                r_secuencia AS ""Secuencia""
            FROM sp_cliente_informacion(@p_codigocliente)";

        var row = await connection.QueryFirstOrDefaultAsync<FotoMedidorHeaderRow>(
            new CommandDefinition(sql, new { p_codigocliente = clave.Trim() }, cancellationToken: ct));

        return row is null
            ? null
            : new ClienteFotoMedidorHeaderDto(
                row.CodigoCliente ?? clave,
                row.NombreCliente ?? string.Empty,
                row.DireccionCliente,
                row.NumeroMedidor,
                row.Ciclo,
                row.Ruta,
                row.Secuencia);
    }

    public async Task<IReadOnlyList<ClienteFotoMedidorItemDto>> GetFotoMedidorAsync(
        int clienteId,
        DateTime desde,
        DateTime hasta,
        CancellationToken ct = default)
    {
        var clave = await GetClienteClaveAsync(clienteId, ct);
        if (string.IsNullOrWhiteSpace(clave))
        {
            return Array.Empty<ClienteFotoMedidorItemDto>();
        }

        var desdeDate = desde.Date;
        var hastaDate = hasta.Date;
        if (desdeDate > hastaDate)
        {
            (desdeDate, hastaDate) = (hastaDate, desdeDate);
        }

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(ct);
        }

        const string sql = @"
            SELECT
                r_ide AS ""Ide"",
                r_ano AS ""Ano"",
                r_mes AS ""Mes"",
                r_usuario AS ""Usuario""
            FROM sp_obtener_historial_foto_medidor(@p_cliente_clave, @p_desde::date, @p_hasta::date)";

        var rows = await connection.QueryAsync<FotoMedidorRow>(
            new CommandDefinition(sql, new
            {
                p_cliente_clave = clave.Trim(),
                p_desde = desdeDate,
                p_hasta = hastaDate
            }, cancellationToken: ct));

        return rows.Select(r => new ClienteFotoMedidorItemDto(
                r.Ide,
                r.Ano,
                r.Mes,
                r.Usuario))
            .ToList();
    }

    public async Task<byte[]?> GetFotoMedidorImagenAsync(int ide, CancellationToken ct = default)
    {
        var imagen = await _context.historicomedicions
            .AsNoTracking()
            .Where(h => h.ide == ide)
            .Select(h => h.imagenmedidor)
            .FirstOrDefaultAsync(ct);

        return imagen;
    }

    public async Task SetNoCortableAsync(string clave, bool valor, string usuario, string? motivo = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
            throw new ArgumentException("La clave del cliente es obligatoria.", nameof(clave));

        var entity = await _context.cliente_maestros
            .FirstOrDefaultAsync(c => c.maestro_cliente_clave == clave, ct)
            ?? throw new KeyNotFoundException($"El cliente '{clave}' no existe.");

        var valorAnterior = entity.no_cortable;

        entity.no_cortable = valor;
        entity.usuariomodificacion = usuario;
        entity.fechamodificacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);

        var companyId = _currentCompanyService.GetCompanyId();
        _context.cln_cliente_estado_logs.Add(new SIAD.Core.Entities.cln_cliente_estado_log
        {
            company_id     = companyId,
            codigocliente  = clave,
            tipo           = "NO_CORTABLE",
            valor_anterior = valorAnterior,
            valor_nuevo    = valor,
            motivo         = motivo,
            usuario        = usuario,
            fecha          = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified)
        });

        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ClienteEstadoLogItemDto>> GetEstadoLogAsync(string clave, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(clave))
            return Array.Empty<ClienteEstadoLogItemDto>();

        return await _context.cln_cliente_estado_logs
            .AsNoTracking()
            .Where(l => l.codigocliente == clave)
            .OrderByDescending(l => l.fecha)
            .Select(l => new ClienteEstadoLogItemDto(
                l.id,
                l.tipo,
                l.valor_anterior,
                l.valor_nuevo,
                l.motivo,
                l.usuario,
                l.fecha))
            .ToListAsync(ct);
    }

    private static string BuildPeriodo(int? ano, int? mes)
    {
        if (!ano.HasValue || !mes.HasValue)
        {
            return string.Empty;
        }

        return $"{ano.Value:D4}/{mes.Value:D2}";
    }

    private static string? FormatearFecha(DateTime? fecha)
        => fecha.HasValue ? fecha.Value.ToString("dd/MM/yyyy") : "--";

    private sealed class HistoricoConsumoHeaderRow
    {
        public string? CodigoCliente { get; init; }
        public string? NombreCliente { get; init; }
        public string? DireccionCliente { get; init; }
        public string? NumeroMedidor { get; init; }
        public string? Catastro { get; init; }
        public string? Diametro { get; init; }
        public string? Categoria { get; init; }
        public string? TipoTarifa { get; init; }
        public string? Ciclo { get; init; }
        public string? Ruta { get; init; }
        public string? Secuencia { get; init; }
    }

    private sealed class HistoricoConsumoRow
    {
        public DateTime? FechaLectAnterior { get; init; }
        public DateTime? FechaLectActual { get; init; }
        public decimal? LecturaAnterior { get; init; }
        public decimal? LecturaActual { get; init; }
        public decimal? Consumo { get; init; }
        public int? Ano { get; init; }
        public int? Mes { get; init; }
        public decimal? Total { get; init; }
        public string? Condicion { get; init; }
        public string? TipoLectura { get; init; }
        public string? Ajuste { get; init; }
        public string? Observacion { get; init; }
    }

    private sealed class HistoricoConsumoSummaryRow
    {
        public int TotalCount { get; init; }
        public decimal TotalConsumo { get; init; }
    }

    private sealed class FotoMedidorHeaderRow
    {
        public string? CodigoCliente { get; init; }
        public string? NombreCliente { get; init; }
        public string? DireccionCliente { get; init; }
        public string? NumeroMedidor { get; init; }
        public string? Ciclo { get; init; }
        public string? Ruta { get; init; }
        public string? Secuencia { get; init; }
    }

    private sealed class FotoMedidorRow
    {
        public int Ide { get; init; }
        public int Ano { get; init; }
        public int Mes { get; init; }
        public string? Usuario { get; init; }
    }

    private static bool IsClienteClaveUniqueViolation(DbUpdateException ex)
    {
        if (ex.GetBaseException() is not PostgresException pg)
        {
            return false;
        }

        if (pg.SqlState != PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }

        return string.Equals(pg.ConstraintName, "cliente_maestro_unique", StringComparison.OrdinalIgnoreCase)
            || string.Equals(pg.ConstraintName, "ix_cliente_maestro_company_clave", StringComparison.OrdinalIgnoreCase);
    }
    private async Task<string?> GetClienteClaveAsync(int clienteId, CancellationToken ct)
    {
        return await _context.cliente_maestros
            .AsNoTracking()
            .Where(c => c.maestro_cliente_id == clienteId)
            .Select(c => c.maestro_cliente_clave)
            .FirstOrDefaultAsync(ct);
    }

    private static string? Limpiar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    private static string BuildHistoricoConsumoOrderBy(string? sortField, bool sortDesc)
    {
        // NOTE: Whitelist sort fields to keep SQL safe and deterministic for virtual scrolling.
        var direction = sortDesc ? "DESC" : "ASC";

        return sortField switch
        {
            "Periodo" => $"ORDER BY r_ano {direction}, r_mes {direction}",
            "Total" => $"ORDER BY r_total {direction}, r_ano DESC, r_mes DESC",
            "Consumo" => $"ORDER BY r_consumo {direction}, r_ano DESC, r_mes DESC",
            _ => "ORDER BY r_ano DESC, r_mes DESC"
        };
    }

    private const int CicloLongitud = 2;
    private const int LibretaLongitud = 3;
    private const int RutaCodigoLongitud = CicloLongitud + LibretaLongitud; // = 5

    private static string? ConstruirIndicativoRuta(int? cicloId, string? barrioCodigo, string? libreta, string? secuencia)
    {
        var barrio = Limpiar(barrioCodigo) ?? string.Empty;
        var secuenciaLimpia = Limpiar(secuencia);
        var rutaCodigo = ConstruirCodigoRutaDeCicloYLibreta(cicloId, libreta);

        var tieneCiclo = cicloId.HasValue;
        var tieneValores = tieneCiclo
                           || !string.IsNullOrWhiteSpace(barrio)
                           || !string.IsNullOrWhiteSpace(rutaCodigo)
                           || !string.IsNullOrWhiteSpace(secuenciaLimpia);

        if (!tieneValores)
        {
            return null;
        }

        var cicloStr = tieneCiclo ? cicloId.Value.ToString() : string.Empty;
        secuenciaLimpia ??= "----";
        var rutaStr = rutaCodigo ?? string.Empty;
        return $"{cicloStr}-{barrio}-{rutaStr}-{secuenciaLimpia}";
    }

    /// <summary>
    /// Construye el codigo de ruta de 5 digitos = ciclo (2d) + libreta (3d).
    /// Por regla de negocio: prefijo siempre coincide con el ciclo. La "libreta"
    /// son los 3 digitos finales (1..999) que el usuario tipea para identificar
    /// la subruta dentro del ciclo.
    /// Devuelve null si no hay datos suficientes.
    /// Lanza ArgumentException si los inputs estan fuera de rango.
    /// </summary>
    private static string? ConstruirCodigoRutaDeCicloYLibreta(int? cicloId, string? libreta)
    {
        var libretaLimpia = Limpiar(libreta);

        if (!cicloId.HasValue && string.IsNullOrWhiteSpace(libretaLimpia))
        {
            return null;
        }

        if (!cicloId.HasValue)
        {
            throw new ArgumentException(
                "El ciclo es obligatorio cuando se especifica libreta.",
                nameof(cicloId));
        }

        if (cicloId.Value < 1 || cicloId.Value > 99)
        {
            throw new ArgumentException(
                $"El ciclo {cicloId.Value} debe estar entre 1 y 99 (cabe en {CicloLongitud} digitos).",
                nameof(cicloId));
        }

        if (string.IsNullOrWhiteSpace(libretaLimpia))
        {
            // Permitir ciclo sin libreta -> ruta = ciclo + "000" (referencia general del ciclo)
            return cicloId.Value.ToString().PadLeft(CicloLongitud, '0').PadRight(RutaCodigoLongitud, '0');
        }

        if (!libretaLimpia.All(char.IsDigit))
        {
            throw new ArgumentException(
                $"La libreta '{libreta}' debe contener solo digitos.",
                nameof(libreta));
        }

        var cicloStr = cicloId.Value.ToString().PadLeft(CicloLongitud, '0');

        // Caso 1: viene la ruta completa (5 digitos) ya armada -> validar prefijo y conservar
        if (libretaLimpia.Length == RutaCodigoLongitud)
        {
            if (!libretaLimpia.StartsWith(cicloStr, StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"La ruta '{libreta}' no pertenece al ciclo {cicloId.Value}. " +
                    $"Una ruta del ciclo {cicloId.Value} debe comenzar con '{cicloStr}'.",
                    nameof(libreta));
            }
            return libretaLimpia;
        }

        // Caso 2: viene solo la libreta (1..3 digitos) -> padear y prefijar con ciclo
        if (libretaLimpia.Length > LibretaLongitud)
        {
            throw new ArgumentException(
                $"La libreta '{libreta}' debe tener 1-3 digitos (o ser la ruta completa de 5 digitos).",
                nameof(libreta));
        }

        var libretaStr = libretaLimpia.PadLeft(LibretaLongitud, '0');
        return cicloStr + libretaStr;
    }

    private static string? ExtraerLibreta(string? indicativoRuta)
    {
        if (string.IsNullOrWhiteSpace(indicativoRuta))
        {
            return null;
        }

        var partes = indicativoRuta.Split('-', StringSplitOptions.TrimEntries);
        if (partes.Length >= 3)
        {
            var libreta = partes[2];
            return string.IsNullOrWhiteSpace(libreta) ? null : libreta;
        }

        return indicativoRuta.Trim();
    }

    private static string ConstruirNombreCompleto(string nombre, string? apellidos)
    {
        var partes = new[] { nombre, apellidos }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p!.Trim());

        return string.Join(' ', partes);
    }

    private static string? ConstruirObservaciones(string? observaciones, string? numeroConvenio)
    {
        var observacionesLimpias = Limpiar(observaciones);
        var convenioLimpio = Limpiar(numeroConvenio);

        if (string.IsNullOrWhiteSpace(convenioLimpio))
        {
            return observacionesLimpias;
        }

        if (string.IsNullOrWhiteSpace(observacionesLimpias))
        {
            return $"Convenio: {convenioLimpio}";
        }

        return $"{observacionesLimpias} | Convenio: {convenioLimpio}";
    }

    private static void SepararNombreApellidos(string nombreCompleto, out string nombre, out string? apellidos)
    {
        if (string.IsNullOrWhiteSpace(nombreCompleto))
        {
            nombre = string.Empty;
            apellidos = null;
            return;
        }

        var partes = nombreCompleto
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (partes.Length == 1)
        {
            nombre = partes[0];
            apellidos = null;
            return;
        }

        nombre = partes[0];
        apellidos = string.Join(' ', partes.Skip(1));
    }

    private static (string? numeroConvenio, string? observaciones) ExtraerConvenio(string? observaciones)
    {
        if (string.IsNullOrWhiteSpace(observaciones))
        {
            return (null, null);
        }

        const string token = "Convenio:";
        var idx = observaciones.IndexOf(token, StringComparison.OrdinalIgnoreCase);

        if (idx < 0)
        {
            return (null, observaciones);
        }

        var numero = observaciones[(idx + token.Length)..].Trim();
        var antes = observaciones[..idx].Trim();

        if (antes.EndsWith("|", StringComparison.Ordinal))
        {
            antes = antes[..^1].Trim();
        }

        return (string.IsNullOrWhiteSpace(numero) ? null : numero,
                string.IsNullOrWhiteSpace(antes) ? null : antes);
    }

    private static IQueryable<cliente_maestro> ApplySorting(IQueryable<cliente_maestro> query, string? sortField, bool sortDesc)
    {
        IOrderedQueryable<cliente_maestro> ordered = sortField switch
        {
            "Codigo" => sortDesc
                ? query.OrderByDescending(c => c.maestro_cliente_clave)
                : query.OrderBy(c => c.maestro_cliente_clave),
            "Nombre" => sortDesc
                ? query.OrderByDescending(c => c.maestro_cliente_nombre)
                : query.OrderBy(c => c.maestro_cliente_nombre),
            "Barrio" => sortDesc
                ? query.OrderByDescending(c => c.barrio_codigo)
                : query.OrderBy(c => c.barrio_codigo),
            "Activo" => sortDesc
                ? query.OrderByDescending(c => c.estado)
                : query.OrderBy(c => c.estado),
            "Identidad" => sortDesc
                ? query.OrderByDescending(c => c.maestro_cliente_identidad)
                : query.OrderBy(c => c.maestro_cliente_identidad),
            _ => sortDesc
                ? query.OrderByDescending(c => c.maestro_cliente_nombre)
                : query.OrderBy(c => c.maestro_cliente_nombre)
        };

        ordered = sortDesc
            ? ordered.ThenByDescending(c => c.maestro_cliente_id)
            : ordered.ThenBy(c => c.maestro_cliente_id);

        return ordered;
    }

    private static IQueryable<transaccion_abonado> ApplyMovimientosSort(
        IQueryable<transaccion_abonado> query,
        string? sortField,
        bool sortDesc)
    {
        IOrderedQueryable<transaccion_abonado> ordered = sortField switch
        {
            "Fecha" => sortDesc
                ? query.OrderByDescending(t => t.fecha_docu).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.fecha_docu).ThenBy(t => t.ide),
            "Tipo" => sortDesc
                ? query.OrderByDescending(t => t.tipotransaccion).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.tipotransaccion).ThenBy(t => t.ide),
            "Descripcion" => sortDesc
                ? query.OrderByDescending(t => t.descripcion).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.descripcion).ThenBy(t => t.ide),
            "Monto" => sortDesc
                ? query.OrderByDescending(t => (t.creditos ?? 0) - (t.debitos ?? 0)).ThenByDescending(t => t.ide)
                : query.OrderBy(t => (t.creditos ?? 0) - (t.debitos ?? 0)).ThenBy(t => t.ide),
            "Saldo" => sortDesc
                ? query.OrderByDescending(t => t.saldo).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.saldo).ThenBy(t => t.ide),
            _ => sortDesc
                ? query.OrderByDescending(t => t.fecha_docu).ThenByDescending(t => t.ide)
                : query.OrderBy(t => t.fecha_docu).ThenBy(t => t.ide)
        };

        return ordered;
    }
}
