using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using SIAD.Core.DTOs.Ordenes;
using SIAD.Core.Entities;
using SIAD.Data;

namespace SIAD.Services.Ordenes;

public class OrdenesService : IOrdenesService
{
    private readonly SiadDbContext _context;

    private sealed record CatalogoItem(string Codigo, string Descripcion);
    private sealed record EstadoFallback(string Codigo, string Nombre, bool PermiteAsignacion);

    // Semántica del canal legacy (app Mi Orden de Trabajo, backend 8086): la app
    // solo descarga órdenes con estado 'P' Y empleado estampado. 'A' NO significa
    // "asignada" sino atendida — una orden en 'A' desaparece de la app. Por eso
    // el único estado válido al asignar es 'P'.
    private static readonly EstadoFallback[] EstadosFallback =
    {
        new("P", "Pendiente", true),
        new("A", "Atendida", false),
        new("E", "Ejecutada", false),
        new("C", "Cancelada", false)
    };

    public OrdenesService(SiadDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<OrdenTrabajoListItemDto>> GetOrdenesAsync(OrdenTrabajoFilterDto filtro, CancellationToken cancellationToken = default)
    {
        var query = _context.orden_trabajos.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filtro.Departamento))
        {
            var normalized = NormalizeDepartamento(filtro.Departamento);
            var departamentos = new List<string> { filtro.Departamento };
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !departamentos.Any(d => d.Equals(normalized, StringComparison.OrdinalIgnoreCase)))
            {
                departamentos.Add(normalized);
            }

            var tiposDepartamento = await _context.tipo_ds.AsNoTracking()
                .Where(t =>
                    t.tipo != null &&
                    t.depto_appmitrabajo != null &&
                    departamentos.Contains(t.depto_appmitrabajo))
                .Select(t => t.tipo!)
                .ToListAsync(cancellationToken);

            var filtroTipos = departamentos
                .Concat(tiposDepartamento)
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (filtroTipos.Length == 0)
            {
                return Array.Empty<OrdenTrabajoListItemDto>();
            }

            query = query.Where(o => o.tipo != null && filtroTipos.Contains(o.tipo));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Tipo))
        {
            query = query.Where(o => o.tipo != null && o.tipo == filtro.Tipo);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Estado))
        {
            query = query.Where(o => o.estado == filtro.Estado);
        }

        if (!string.IsNullOrWhiteSpace(filtro.ClienteClave))
        {
            query = query.Where(o => o.maestro_cliente_clave == filtro.ClienteClave);
        }

        if (!string.IsNullOrWhiteSpace(filtro.Clave))
        {
            var clavePattern = $"%{filtro.Clave.Trim()}%";
            query = query.Where(o => EF.Functions.ILike(o.maestro_cliente_clave, clavePattern));
        }

        if (!string.IsNullOrWhiteSpace(filtro.ClienteNombre))
        {
            // Filtra por nombre del cliente. orden_trabajo solo guarda la clave, así que se
            // correlaciona con cliente_maestro (tenant-scoped: el filtro global de company
            // limita a clientes de la empresa actual).
            var nombrePattern = $"%{filtro.ClienteNombre.Trim()}%";
            query = query.Where(o => _context.cliente_maestros
                .Any(c => c.maestro_cliente_clave == o.maestro_cliente_clave
                          && EF.Functions.ILike(c.maestro_cliente_nombre, nombrePattern)));
        }

        if (!string.IsNullOrWhiteSpace(filtro.Busqueda))
        {
            var pattern = $"%{filtro.Busqueda.Trim()}%";
            query = query.Where(o =>
                EF.Functions.ILike(o.maestro_cliente_clave, pattern) ||
                EF.Functions.ILike(o.concepto, pattern) ||
                (o.empleado != null && EF.Functions.ILike(o.empleado, pattern)) ||
                (o.usuario != null && EF.Functions.ILike(o.usuario, pattern)));
        }

        if (filtro.FechaDesde.HasValue)
        {
            var desde = DateOnly.FromDateTime(filtro.FechaDesde.Value.Date);
            query = query.Where(o => o.fecha >= desde);
        }

        if (filtro.FechaHasta.HasValue)
        {
            var hasta = DateOnly.FromDateTime(filtro.FechaHasta.Value.Date);
            query = query.Where(o => o.fecha <= hasta);
        }

        if (filtro.Anio.HasValue)
        {
            query = query.Where(o => o.ano == filtro.Anio.Value);
        }

        if (filtro.Mes.HasValue)
        {
            query = query.Where(o => o.mes == filtro.Mes.Value);
        }

        var ordenes = await query
            .OrderByDescending(o => o.fecha)
            .ThenByDescending(o => o.orden_numero)
            .ToListAsync(cancellationToken);

        if (ordenes.Count == 0)
        {
            return Array.Empty<OrdenTrabajoListItemDto>();
        }

        var claves = ordenes.Select(o => o.maestro_cliente_clave).Distinct().ToArray();

        var clientes = await _context.cliente_maestros.AsNoTracking()
            .Where(c => claves.Contains(c.maestro_cliente_clave))
            .Select(c => new
            {
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.maestro_cliente_id
            })
            .ToListAsync(cancellationToken);

        var clientePorClave = clientes.ToDictionary(c => c.maestro_cliente_clave, c => c);
        var clienteIds = clientes.Select(c => c.maestro_cliente_id).ToArray();

        var detalles = await _context.cliente_detalles.AsNoTracking()
            .Where(d => clienteIds.Contains(d.maestro_cliente_id))
            .ToListAsync(cancellationToken);

        var direccionPorCliente = detalles
            .GroupBy(d => d.maestro_cliente_id)
            .ToDictionary(
                g => g.Key,
                g => g.OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                      .Select(d => d.detalle_cliente_direccion)
                      .FirstOrDefault());

        var tipoCodigos = ordenes
            .Where(o => !string.IsNullOrWhiteSpace(o.tipo))
            .Select(o => o.tipo!)
            .Distinct()
            .ToArray();

        var tiposCatalogoLista = tipoCodigos.Length == 0
            ? new List<CatalogoItem>()
            : await _context.tipo_ds.AsNoTracking()
                .Where(t => t.tipo != null && tipoCodigos.Contains(t.tipo))
                .Select(t => new CatalogoItem(
                    t.tipo!,
                    string.IsNullOrWhiteSpace(t.descripcion) ? t.tipo! : t.descripcion!))
                .Distinct()
                .ToListAsync(cancellationToken);

        var tiposCatalogo = tiposCatalogoLista
            .GroupBy(t => t.Codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First().Descripcion, StringComparer.OrdinalIgnoreCase);

        var estadoCodigos = ordenes
            .Where(o => !string.IsNullOrWhiteSpace(o.estado))
            .Select(o => o.estado!)
            .Distinct()
            .ToArray();

        var estadosCatalogoLista = estadoCodigos.Length == 0
            ? new List<orden_trabajo_estado>()
            : await _context.orden_trabajo_estados.AsNoTracking()
                .Where(e => estadoCodigos.Contains(e.codigo))
                .ToListAsync(cancellationToken);

        var estadosCatalogo = estadosCatalogoLista
            .GroupBy(e => e.codigo, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        return ordenes.Select(o =>
        {
            var fecha = o.fecha.ToDateTime(TimeOnly.MinValue);
            var estadoCodigo = NormalizeEstadoCodigo(o.estado);
            clientePorClave.TryGetValue(o.maestro_cliente_clave, out var cliente);
            string propietario = cliente?.maestro_cliente_nombre ?? o.maestro_cliente_clave;
            string? direccion = null;
            if (cliente != null && direccionPorCliente.TryGetValue(cliente.maestro_cliente_id, out var dir))
            {
                direccion = dir;
            }

            string? tipoDescripcion = null;
            if (!string.IsNullOrWhiteSpace(o.tipo) && tiposCatalogo.TryGetValue(o.tipo, out var tipoNombre))
            {
                tipoDescripcion = tipoNombre;
            }

            tipoDescripcion ??= MapTipoDescripcion(o.tipo);

            string? estadoDescripcion = null;
            orden_trabajo_estado? estadoCatalogo = null;
            if (!string.IsNullOrWhiteSpace(estadoCodigo) && estadosCatalogo.TryGetValue(estadoCodigo, out var estadoDto))
            {
                estadoCatalogo = estadoDto;
                estadoDescripcion = estadoDto.nombre;
            }

            estadoDescripcion ??= MapEstadoDescripcion(estadoCodigo);

            return new OrdenTrabajoListItemDto(
                o.orden_id,
                o.orden_numero,
                o.maestro_cliente_clave,
                propietario,
                direccion,
                fecha,
                o.fecha_creacion,
                o.concepto,
                o.empleado,
                o.tipo,
                tipoDescripcion,
                estadoCatalogo?.codigo ?? estadoCodigo ?? string.Empty,
                estadoDescripcion,
                o.usuario);
        }).ToList();
    }

    public async Task<OrdenTrabajoDetailDto?> GetOrdenAsync(int id, CancellationToken cancellationToken = default)
    {
        var orden = await _context.orden_trabajos.AsNoTracking()
            .FirstOrDefaultAsync(o => o.orden_id == id, cancellationToken);

        if (orden is null)
        {
            return null;
        }

        var cliente = await _context.cliente_maestros.AsNoTracking()
            .Where(c => c.maestro_cliente_clave == orden.maestro_cliente_clave)
            .Select(c => new
            {
                c.maestro_cliente_nombre,
                c.maestro_cliente_id
            })
            .FirstOrDefaultAsync(cancellationToken);

        string propietario = cliente?.maestro_cliente_nombre ?? orden.maestro_cliente_clave;
        string? direccion = null;

        if (cliente != null)
        {
            direccion = await _context.cliente_detalles.AsNoTracking()
                .Where(d => d.maestro_cliente_id == cliente.maestro_cliente_id)
                .OrderByDescending(d => d.fechamodificacion ?? d.fechacreacion)
                .Select(d => d.detalle_cliente_direccion)
                .FirstOrDefaultAsync(cancellationToken);
        }

        var adjuntos = await _context.orden_trabajo_adjuntos.AsNoTracking()
            .Where(a => a.numeroorden == orden.orden_numero.ToString())
            .OrderBy(a => a.id)
            .Select(a => new OrdenTrabajoAdjuntoDto(
                a.id,
                a.nombre,
                a.tipo,
                a.latitud,
                a.longitud,
                a.fechainicio,
                a.fechafin,
                a.fechaobtenerordenes))
            .ToListAsync(cancellationToken);

        var materiales = await _context.ordent_mates.AsNoTracking()
            .Where(m => m.numero == orden.orden_numero)
            .OrderBy(m => m.descripcion)
            .Select(m => new OrdenTrabajoMaterialDto(
                m.id,
                m.cuenta,
                m.codproduc,
                m.descripcion,
                m.cantidad,
                m.fecha))
            .ToListAsync(cancellationToken);

        var fecha = orden.fecha.ToDateTime(TimeOnly.MinValue);

        string? tipoDescripcion = null;
        if (!string.IsNullOrWhiteSpace(orden.tipo))
        {
            tipoDescripcion = await _context.tipo_ds.AsNoTracking()
                .Where(t => t.tipo == orden.tipo)
                .Select(t => string.IsNullOrWhiteSpace(t.descripcion) ? t.tipo! : t.descripcion!)
                .FirstOrDefaultAsync(cancellationToken);
        }

        tipoDescripcion ??= MapTipoDescripcion(orden.tipo);

        var estadoCodigo = NormalizeEstadoCodigo(orden.estado);
        string? estadoDescripcion = null;
        orden_trabajo_estado? estadoCatalogo = null;
        if (!string.IsNullOrWhiteSpace(estadoCodigo))
        {
            estadoCatalogo = await _context.orden_trabajo_estados.AsNoTracking()
                .FirstOrDefaultAsync(e => e.codigo == estadoCodigo, cancellationToken);
            estadoDescripcion = estadoCatalogo?.nombre;
        }

        estadoDescripcion ??= MapEstadoDescripcion(estadoCodigo);

        return new OrdenTrabajoDetailDto(
            orden.orden_id,
            orden.orden_numero,
            orden.maestro_cliente_clave,
            propietario,
            direccion,
            fecha,
            orden.fecha_creacion,
            orden.concepto,
            estadoCatalogo?.codigo ?? estadoCodigo ?? string.Empty,
            estadoDescripcion,
            orden.empleado,
            orden.usuario,
            orden.personas,
            orden.tipo,
            tipoDescripcion,
            orden.saldo,
            orden.informe,
            adjuntos,
            materiales);
    }

    public async Task<OrdenTrabajoOperacionResultadoDto> CrearOrdenAsync(CrearOrdenTrabajoDto dto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(dto.ClienteClave))
        {
            return new OrdenTrabajoOperacionResultadoDto(false, "La clave del cliente es obligatoria.");
        }

        var fecha = DateOnly.FromDateTime(dto.Fecha.Date);
        var creacion = DateTime.SpecifyKind(DateTime.UtcNow, DateTimeKind.Unspecified);
        var numero = dto.Numero ?? await _context.orden_trabajos.AsNoTracking()
            .Select(o => (int?)o.orden_numero)
            .MaxAsync(cancellationToken) + 1 ?? 1;

        var existeNumero = await _context.orden_trabajos.AsNoTracking()
            .AnyAsync(o => o.orden_numero == numero, cancellationToken);

        if (existeNumero)
        {
            return new OrdenTrabajoOperacionResultadoDto(false, $"El número de orden {numero} ya existe.", numero);
        }




        var entidad = new Core.Entities.orden_trabajo
        {
            orden_numero = numero,
            maestro_cliente_clave = dto.ClienteClave.Trim(),
            concepto = dto.Concepto.Trim(),
            estado = string.IsNullOrWhiteSpace(dto.Estado) ? "P" : dto.Estado.Trim(),
            fecha = fecha,
            fecha_creacion = creacion,
            personas = dto.Personas,
            tipo = dto.Tipo,
            empleado = dto.Empleado,
            usuario = dto.Usuario,
            ano = dto.Anio ?? fecha.Year,
            mes = dto.Mes ?? fecha.Month,
            saldo = dto.Saldo
        };

        _context.orden_trabajos.Add(entidad);
        await _context.SaveChangesAsync(cancellationToken);

        return new OrdenTrabajoOperacionResultadoDto(true, $"Orden {entidad.orden_numero} creada correctamente.", entidad.orden_numero);
    }

    public async Task<OrdenTrabajoOperacionResultadoDto> AsignarOrdenesAsync(OrdenTrabajoAsignacionDto dto, CancellationToken cancellationToken = default)
    {
        if (dto.NumerosOrden.Count == 0)
        {
            return new OrdenTrabajoOperacionResultadoDto(false, "Debe seleccionar al menos una orden para asignar.");
        }

        var ordenes = await _context.orden_trabajos
            .Where(o => dto.NumerosOrden.Contains(o.orden_numero))
            .ToListAsync(cancellationToken);

        if (ordenes.Count == 0)
        {
            return new OrdenTrabajoOperacionResultadoDto(false, "No se encontraron órdenes con los números proporcionados.");
        }

        foreach (var orden in ordenes)
        {
            orden.usuario = dto.Usuario;
            if (!string.IsNullOrWhiteSpace(dto.Empleado))
            {
                orden.empleado = dto.Empleado;
            }
            else if (string.IsNullOrWhiteSpace(orden.empleado))
            {
                orden.empleado = dto.Usuario;
            }

            // El canal de la app exige estado 'P' + empleado para que la orden
            // se descargue; sin estado explícito la asignación queda en 'P'.
            orden.estado = string.IsNullOrWhiteSpace(dto.Estado) ? "P" : dto.Estado.Trim();
        }

        await _context.SaveChangesAsync(cancellationToken);

        return new OrdenTrabajoOperacionResultadoDto(
            true,
            $"Se asignaron {ordenes.Count} orden(es) al usuario {dto.Usuario}.",
            null);
    }

    public async Task<IReadOnlyList<UsuarioMiOrdenDto>> GetUsuariosMiOrdenAsync(int? tipo, CancellationToken cancellationToken = default)
    {
        var query = _context.usuarios_miordens.AsNoTracking();

        if (tipo.HasValue)
        {
            query = query.Where(u => u.tipo == tipo.Value);
        }

        var usuarios = await query
            .OrderBy(u => u.nombre)
            .ToListAsync(cancellationToken);

        return usuarios.Select(u =>
        {
            var activo = ToBoolean(u.estado);
            return new UsuarioMiOrdenDto(
                u.id,
                u.nombre,
                u.usuario,
                u.clave,
                u.tipo,
                MapTipoDescripcionFromInt(u.tipo),
                activo,
                activo ? "Activo" : "Inactivo");
        }).ToList();
    }

    public async Task<IReadOnlyList<OrdenTrabajoTipoDto>> BuscarTiposOrdenAsync(string departamentoAplicacion, string? texto, int take, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(departamentoAplicacion))
        {
            return Array.Empty<OrdenTrabajoTipoDto>();
        }

        var normalized = NormalizeDepartamento(departamentoAplicacion);
        var query = _context.tipo_ds.AsNoTracking()
            .Where(t =>
                t.depto_appmitrabajo == departamentoAplicacion ||
                t.depto_appmitrabajo == normalized);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var pattern = $"%{texto.Trim()}%";
            query = query.Where(t =>
                (t.descripcion != null && EF.Functions.ILike(t.descripcion, pattern)) ||
                (t.tipo != null && EF.Functions.ILike(t.tipo, pattern)));
        }

        var limite = Math.Clamp(take, 1, 100);

        var tipos = await query
            .OrderBy(t => t.descripcion ?? t.tipo)
            .ThenBy(t => t.tipo)
            .Take(limite)
            .ToListAsync(cancellationToken);

        return tipos.Select(t => new OrdenTrabajoTipoDto(
            t.tipo ?? string.Empty,
            t.descripcion ?? t.tipo ?? string.Empty,
            t.depto_appmitrabajo)).ToList();
    }

    public async Task<IReadOnlyList<OrdenTrabajoPropietarioDto>> BuscarPropietariosAsync(string? texto, int take, CancellationToken cancellationToken = default)
    {
        var baseQuery = _context.cliente_maestros.AsNoTracking()
            .Where(c => c.estado);

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var pattern = $"%{texto.Trim()}%";
            baseQuery = baseQuery.Where(c =>
                EF.Functions.ILike(c.maestro_cliente_clave, pattern) ||
                EF.Functions.ILike(c.maestro_cliente_nombre, pattern));
        }

        var limite = Math.Clamp(take, 1, 100);

        var propietariosBase = await baseQuery
            .OrderBy(c => c.maestro_cliente_nombre)
            .Take(limite)
            .Select(c => new
            {
                c.maestro_cliente_id,
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                c.estado
            })
            .ToListAsync(cancellationToken);

        if (propietariosBase.Count == 0)
        {
            return Array.Empty<OrdenTrabajoPropietarioDto>();
        }

        var ids = propietariosBase.Select(c => c.maestro_cliente_id).ToArray();

        var detalles = await _context.cliente_detalles.AsNoTracking()
            .Where(d => ids.Contains(d.maestro_cliente_id))
            .Select(d => new
            {
                d.maestro_cliente_id,
                d.detalle_cliente_direccion,
                d.fechamodificacion,
                d.fechacreacion
            })
            .ToListAsync(cancellationToken);

        var direccionPorCliente = detalles
            .GroupBy(d => d.maestro_cliente_id)
            .ToDictionary(
                g => g.Key,
                g => g
                    .OrderByDescending(d => (d.fechamodificacion ?? d.fechacreacion) ?? DateTime.MinValue)
                    .Select(d => d.detalle_cliente_direccion)
                    .FirstOrDefault());

        return propietariosBase.Select(c =>
        {
            direccionPorCliente.TryGetValue(c.maestro_cliente_id, out var direccion);
            return new OrdenTrabajoPropietarioDto(
                c.maestro_cliente_clave,
                c.maestro_cliente_nombre,
                direccion,
                c.estado);
        }).ToList();
    }

    public async Task<IReadOnlyList<OrdenTrabajoEstadoDto>> BuscarEstadosOrdenAsync(string? texto, int take, CancellationToken cancellationToken = default)
    {
        var query = _context.orden_trabajo_estados.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(texto))
        {
            var pattern = $"%{texto.Trim()}%";
            query = query.Where(e =>
                (e.nombre != null && EF.Functions.ILike(e.nombre, pattern)) ||
                EF.Functions.ILike(e.codigo, pattern));
        }

        var limite = Math.Clamp(take, 1, 100);

        var estados = await query
            .OrderBy(e => e.nombre ?? e.codigo)
            .Take(limite)
            .ToListAsync(cancellationToken);

        if (estados.Count == 0)
        {
            IEnumerable<EstadoFallback> fallback = EstadosFallback;
            if (!string.IsNullOrWhiteSpace(texto))
            {
                fallback = fallback.Where(e =>
                    e.Nombre.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    e.Codigo.Contains(texto.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            return fallback
                .Take(limite)
                .Select(e => new OrdenTrabajoEstadoDto(e.Codigo, e.Nombre, e.PermiteAsignacion))
                .ToList();
        }

        return estados
            .Select(e => new OrdenTrabajoEstadoDto(
                e.codigo,
                string.IsNullOrWhiteSpace(e.nombre) ? e.codigo : e.nombre,
                e.permite_asignacion))
            .ToList();
    }

    public async Task<IReadOnlyList<CoordenadaOrdenDto>> GetCoordenadasAsync(CancellationToken cancellationToken = default)
    {
        var coordenadas = await (
            from coord in _context.coordenadas_empleados.AsNoTracking()
            join usuario in _context.usuarios_miordens.AsNoTracking()
                on coord.nombre equals usuario.usuario into usuarios
            from usuario in usuarios.DefaultIfEmpty()
            select new
            {
                Nombre = usuario != null
                    ? (usuario.nombre ?? string.Empty)
                    : (coord.nombre ?? string.Empty),
                Usuario = usuario != null
                    ? (usuario.usuario ?? (coord.nombre ?? string.Empty))
                    : (coord.nombre ?? string.Empty),
                Tipo = usuario != null
                    ? usuario.tipo
                    : 0,
                coord.fecha,
                coord.latitud,
                coord.longitud
            }).ToListAsync(cancellationToken);

        return coordenadas.Select(c => new CoordenadaOrdenDto(
            c.Nombre,
            c.Usuario,
            c.Tipo,
            c.fecha,
            TryParseDecimal(c.latitud),
            TryParseDecimal(c.longitud),
            MapTipoDescripcionFromInt(c.Tipo))).ToList();
    }

    private static string NormalizeDepartamento(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return value.TrimStart('0');
    }

    private static string MapTipoDescripcion(string? tipo)
    {
        return tipo switch
        {
            "01" => "Órden Trabajo Agua",
            "1" => "Órden Trabajo Agua",
            "02" => "Órden Trabajo Alcantarillado",
            "2" => "Órden Trabajo Alcantarillado",
            "03" => "Órden de Corte",
            "3" => "Órden de Corte",
            _ => string.IsNullOrWhiteSpace(tipo) ? "Sin tipo" : tipo
        };
    }

    private static string MapTipoDescripcionFromInt(int tipo)
    {
        return tipo switch
        {
            1 => "Órden Trabajo Agua",
            2 => "Órden de Corte",
            3 => "Órden Trabajo Alcantarillado",
            _ => $"Tipo {tipo}"
        };
    }

    private static string MapEstadoDescripcion(string? estado)
    {
        var codigo = NormalizeEstadoCodigo(estado);
        return codigo switch
        {
            "P" => "Pendiente",
            "A" => "Atendida",
            "E" => "Ejecutada",
            "C" => "Cancelada",
            _ => string.IsNullOrWhiteSpace(codigo) ? "Sin estado" : codigo
        };
    }

    private static string? NormalizeEstadoCodigo(string? estado)
    {
        return string.IsNullOrWhiteSpace(estado) ? null : estado.Trim().ToUpperInvariant();
    }

    private static bool ToBoolean(BitArray? bitArray)
    {
        return bitArray != null && bitArray.Length > 0 && bitArray[0];
    }

    private static decimal? TryParseDecimal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
        {
            return result;
        }

        if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture, out result))
        {
            return result;
        }

        return null;
    }
}
